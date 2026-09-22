using System.Data;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.AccountMappings;
using MiniErp.Application.Features.CashboxRevaluations;
using MiniErp.Application.Features.FiscalYears;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using static MiniErp.Application.Features.CashboxRevaluations.CashboxRevaluationErrors;

namespace MiniErp.Infrastructure.Services.CashboxRevaluations;

public sealed class CashboxRevaluationService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext,
    IAccountMappingResolver accountMappingResolver,
    IAutomaticPostingService automaticPostingService,
    TimeProvider timeProvider) : ICashboxRevaluationService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<CashboxRevaluationResponse>> CreateAsync(
        CashboxRevaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.CashboxId <= 0)
        {
            return Result<CashboxRevaluationResponse>.Failure(
                InvalidRequest());
        }

        if (!ExchangeRateRules.IsValidRate(request.ClosingRate))
        {
            return Result<CashboxRevaluationResponse>.Failure(RateInvalid());
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var cashbox = await dbContext.Cashboxes
            .IgnoreQueryFilters()
            .Where(row => row.CompanyId == companyId && row.Id == request.CashboxId && !row.IsDeleted)
            .Select(row => new { row.Id, row.Name, row.Currency, row.OpeningBalanceDate, BaseCurrency = row.Company.Settings == null ? CurrencyCode.EGP : row.Company.Settings.BaseCurrency })
            .SingleOrDefaultAsync(cancellationToken);
        if (cashbox is null)
        {
            return Result<CashboxRevaluationResponse>.Failure(CashboxNotFound(request.CashboxId));
        }

        if (cashbox.Currency == cashbox.BaseCurrency)
        {
            return Result<CashboxRevaluationResponse>.Failure(BaseCurrencyCashbox());
        }

        if (request.RevaluationDate < cashbox.OpeningBalanceDate)
        {
            return Result<CashboxRevaluationResponse>.Failure(
                BeforeOpeningBalance(request.RevaluationDate));
        }

        if (await dbContext.CashboxRevaluations.AnyAsync(row =>
                row.CompanyId == companyId && row.CashboxId == request.CashboxId &&
                row.RevaluationDate == request.RevaluationDate && !row.IsDeleted, cancellationToken))
        {
            return Result<CashboxRevaluationResponse>.Failure(Duplicate(request.CashboxId, request.RevaluationDate));
        }

        if (await dbContext.CashboxRevaluations.AnyAsync(row =>
                row.CompanyId == companyId && row.CashboxId == request.CashboxId &&
                row.RevaluationDate > request.RevaluationDate && !row.IsDeleted, cancellationToken))
        {
            return Result<CashboxRevaluationResponse>.Failure(Backdated(request.RevaluationDate));
        }

        var fiscalYear = await dbContext.FiscalYears
            .AsNoTracking()
            .Where(year => year.CompanyId == companyId && year.StartDate <= request.RevaluationDate && year.EndDate >= request.RevaluationDate)
            .Select(year => new { year.Id, year.Status })
            .SingleOrDefaultAsync(cancellationToken);
        if (fiscalYear is null || fiscalYear.Status != FiscalYearStatus.Open)
        {
            return Result<CashboxRevaluationResponse>.Failure(FiscalYearClosed());
        }

        var lines = await dbContext.JournalEntryLines
            .AsNoTracking()
            .Where(line => line.CompanyId == companyId &&
                line.PartyType == JournalPartyType.Cashbox && line.PartyId == request.CashboxId &&
                !line.IsDeleted && !line.JournalEntry.IsDeleted &&
                line.JournalEntry.Status == JournalEntryStatus.Posted &&
                line.JournalEntry.ReversalOfEntryId == null &&
                line.JournalEntry.ReversedOn == null &&
                line.JournalEntry.EntryDate <= request.RevaluationDate)
            .Select(line => new
            {
                line.Currency,
                line.TransactionDebit,
                line.TransactionCredit,
                line.Debit,
                line.Credit,
                SourceType = line.JournalEntry.SourceType
            })
            .ToListAsync(cancellationToken);

        var foreignAmount = ExchangeRateRules.RoundBaseAmount(lines
            .Where(line => line.Currency == cashbox.Currency && line.SourceType != JournalEntrySourceType.CashboxRevaluation)
            .Sum(line => line.TransactionDebit - line.TransactionCredit));
        var carryingBaseAmount = ExchangeRateRules.RoundBaseAmount(lines
            .Sum(line => line.Debit - line.Credit));
        if (foreignAmount < 0m || carryingBaseAmount < 0m)
        {
            return Result<CashboxRevaluationResponse>.Failure(
                NegativeBalance());
        }

        // Ledger lines are stored at four decimal places.  Keep the reported
        // carrying amount precise, but round the target and posted delta to
        // the same journal precision so the next period starts from exactly
        // what was posted.
        var targetBaseAmount = decimal.Round(
            ExchangeRateRules.ConvertToBase(foreignAmount, request.ClosingRate),
            4,
            MidpointRounding.AwayFromZero);
        var delta = decimal.Round(
            targetBaseAmount - carryingBaseAmount,
            4,
            MidpointRounding.AwayFromZero);

        var cashboxAccount = await accountMappingResolver.ResolveAsync(fiscalYear.Id, AccountingMappingType.Cashbox, request.CashboxId, cancellationToken);
        if (cashboxAccount.IsFailure)
        {
            return Result<CashboxRevaluationResponse>.Failure(cashboxAccount.Errors);
        }

        int? journalEntryId = null;
        string? journalEntryNumber = null;
        var row = new CashboxRevaluation
        {
            CompanyId = companyId,
            CashboxId = request.CashboxId,
            RevaluationDate = request.RevaluationDate,
            ClosingRate = ExchangeRateRules.RoundRate(request.ClosingRate),
            ForeignAmount = foreignAmount,
            CarryingBaseAmount = carryingBaseAmount,
            TargetBaseAmount = targetBaseAmount,
            DeltaBaseAmount = delta,
            CreatedOn = timeProvider.GetUtcNow().UtcDateTime
        };
        dbContext.CashboxRevaluations.Add(row);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (delta != 0m)
        {
            var mappingType = delta > 0m ? AccountingMappingType.ExchangeGain : AccountingMappingType.ExchangeLoss;
            var differenceAccount = await accountMappingResolver.ResolveAsync(fiscalYear.Id, mappingType, cancellationToken: cancellationToken);
            if (differenceAccount.IsFailure)
            {
                return Result<CashboxRevaluationResponse>.Failure(AccountMappingMissing());
            }

            var amount = Math.Abs(delta);
            var cashboxLine = new JournalEntryLineRequest(
                AccountId: cashboxAccount.Value,
                Description: "إعادة تقييم خزينة",
                Debit: delta > 0m ? amount : 0m,
                Credit: delta < 0m ? amount : 0m,
                PartyType: JournalPartyType.Cashbox,
                PartyId: request.CashboxId,
                Currency: cashbox.BaseCurrency,
                ExchangeRate: 1m,
                TransactionDebit: delta > 0m ? amount : 0m,
                TransactionCredit: delta < 0m ? amount : 0m);
            var differenceLine = new JournalEntryLineRequest(
                AccountId: differenceAccount.Value,
                Description: delta > 0m ? "أرباح فروق إعادة تقييم العملة" : "خسائر فروق إعادة تقييم العملة",
                Debit: delta < 0m ? amount : 0m,
                Credit: delta > 0m ? amount : 0m,
                Currency: cashbox.BaseCurrency,
                ExchangeRate: 1m,
                TransactionDebit: delta < 0m ? amount : 0m,
                TransactionCredit: delta > 0m ? amount : 0m);
            var posting = await automaticPostingService.CreateOrUpdateAsync(
                new AutomaticJournalEntryRequest(
                    FiscalYearId: fiscalYear.Id,
                    EntryDate: request.RevaluationDate,
                    Description: $"إعادة تقييم خزينة {cashbox.Name}",
                    SourceType: JournalEntrySourceType.CashboxRevaluation,
                    SourceId: row.Id,
                    SourceNumber: $"CBR-{row.Id}",
                    Lines: [cashboxLine, differenceLine]), cancellationToken);
            if (posting.IsFailure)
            {
                return Result<CashboxRevaluationResponse>.Failure(posting.Errors);
            }
            journalEntryId = posting.Value.JournalEntryId;
            journalEntryNumber = posting.Value.EntryNumber;
            row.JournalEntryId = journalEntryId;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return Result<CashboxRevaluationResponse>.Success(ToResponse(
            row,
            cashbox.Name,
            cashbox.Currency,
            journalEntryId,
            journalEntryNumber));
    }

    public async Task<Result<IReadOnlyList<CashboxRevaluationResponse>>> GetAsync(
        int? cashboxId = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.CashboxRevaluations
            .AsNoTracking()
            .Where(row => row.CompanyId == companyId && !row.IsDeleted &&
                (!cashboxId.HasValue || row.CashboxId == cashboxId.Value) &&
                (!fromDate.HasValue || row.RevaluationDate >= fromDate.Value) &&
                (!toDate.HasValue || row.RevaluationDate <= toDate.Value))
            .OrderByDescending(row => row.RevaluationDate)
            .ThenByDescending(row => row.Id)
            .Select(row => new CashboxRevaluationResponse(
                Id: row.Id,
                CashboxId: row.CashboxId,
                CashboxName: row.Cashbox.Name,
                Currency: row.Cashbox.Currency,
                RevaluationDate: row.RevaluationDate,
                ClosingRate: row.ClosingRate,
                ForeignAmount: row.ForeignAmount,
                CarryingBaseAmount: row.CarryingBaseAmount,
                TargetBaseAmount: row.TargetBaseAmount,
                DeltaBaseAmount: row.DeltaBaseAmount,
                JournalEntryId: row.JournalEntryId,
                JournalEntryNumber: row.JournalEntry == null ? string.Empty : row.JournalEntry.EntryNumber))
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<CashboxRevaluationResponse>>.Success(rows);
    }

    private static CashboxRevaluationResponse ToResponse(
        CashboxRevaluation row,
        string cashboxName,
        CurrencyCode currency,
        int? journalEntryId,
        string? journalEntryNumber) =>
        new(
            Id: row.Id,
            CashboxId: row.CashboxId,
            CashboxName: cashboxName,
            Currency: currency,
            RevaluationDate: row.RevaluationDate,
            ClosingRate: row.ClosingRate,
            ForeignAmount: row.ForeignAmount,
            CarryingBaseAmount: row.CarryingBaseAmount,
            TargetBaseAmount: row.TargetBaseAmount,
            DeltaBaseAmount: row.DeltaBaseAmount,
            JournalEntryId: journalEntryId,
            JournalEntryNumber: journalEntryNumber ?? string.Empty);
}
