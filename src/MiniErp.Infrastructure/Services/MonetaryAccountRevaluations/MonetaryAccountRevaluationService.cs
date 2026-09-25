using System.Data;
using Microsoft.EntityFrameworkCore;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.AccountMappings;
using MiniErp.Application.Features.FiscalYears;
using MiniErp.Application.Features.JournalEntries;
using MiniErp.Application.Features.MonetaryAccountRevaluations;
using MiniErp.Domain.Entities.Accounting;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using static MiniErp.Application.Features.MonetaryAccountRevaluations.MonetaryAccountRevaluationErrors;

namespace MiniErp.Infrastructure.Services.MonetaryAccountRevaluations;

public sealed class MonetaryAccountRevaluationService(
    ApplicationDbContext dbContext,
    ICurrentCompanyContext currentCompanyContext,
    IAccountMappingResolver accountMappingResolver,
    IAutomaticPostingService automaticPostingService,
    TimeProvider timeProvider) : IMonetaryAccountRevaluationService, IScopedService
{
    private readonly int companyId = currentCompanyContext.CompanyId;

    public async Task<Result<IReadOnlyList<MonetaryAccountRevaluationOption>>> GetOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        var baseCurrency = await GetBaseCurrencyAsync(cancellationToken);
        var rows = await dbContext.JournalEntryLines
            .AsNoTracking()
            .Where(line => line.CompanyId == companyId &&
                line.Currency != baseCurrency &&
                !line.IsDeleted && !line.JournalEntry.IsDeleted &&
                line.JournalEntry.Status == JournalEntryStatus.Posted &&
                line.JournalEntry.ReversalOfEntryId == null &&
                line.JournalEntry.ReversedOn == null &&
                (line.PartyType == null ||
                 line.PartyType == JournalPartyType.Customer ||
                 line.PartyType == JournalPartyType.Supplier ||
                 line.PartyType == JournalPartyType.Cashbox))
            .Select(line => new
            {
                line.AccountId,
                AccountCode = line.Account.Code,
                AccountName = line.Account.Name,
                line.Currency,
                line.PartyType,
                line.PartyId,
                ForeignAmount = line.TransactionDebit - line.TransactionCredit
            })
            .ToListAsync(cancellationToken);

        var options = rows
            .GroupBy(row => new
            {
                row.AccountId,
                row.AccountCode,
                row.AccountName,
                row.Currency,
                row.PartyType,
                row.PartyId
            })
            .Where(group => group.Sum(row => row.ForeignAmount) != 0m)
            .Select(group => new MonetaryAccountRevaluationOption(
                AccountId: group.Key.AccountId,
                AccountCode: group.Key.AccountCode,
                AccountName: group.Key.AccountName,
                Currency: group.Key.Currency,
                PartyType: group.Key.PartyType,
                PartyId: group.Key.PartyId,
                PartyCode: null,
                PartyName: null))
            .OrderBy(row => row.AccountCode)
            .ThenBy(row => row.Currency)
            .ThenBy(row => row.PartyType)
            .ThenBy(row => row.PartyId)
            .ToArray();

        var decorated = await DecorateOptionsAsync(options, cancellationToken);
        return Result<IReadOnlyList<MonetaryAccountRevaluationOption>>.Success(decorated);
    }

    public async Task<Result<MonetaryAccountRevaluationResponse>> CreateAsync(
        MonetaryAccountRevaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.AccountId <= 0 || request.PartyId is <= 0 ||
            (request.PartyType is null) != (request.PartyId is null))
            return Result<MonetaryAccountRevaluationResponse>.Failure(InvalidRequest());
        if (!ExchangeRateRules.IsValidRate(request.ClosingRate))
            return Result<MonetaryAccountRevaluationResponse>.Failure(RateInvalid());
        if (request.PartyType is not null &&
            request.PartyType is not (JournalPartyType.Customer or JournalPartyType.Supplier or JournalPartyType.Cashbox))
            return Result<MonetaryAccountRevaluationResponse>.Failure(UnsupportedParty());

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var baseCurrency = await GetBaseCurrencyAsync(cancellationToken);
        if (request.Currency == baseCurrency)
            return Result<MonetaryAccountRevaluationResponse>.Failure(BaseCurrencyAccount());

        var account = await dbContext.Accounts
            .AsNoTracking()
            .Where(row => row.CompanyId == companyId && row.Id == request.AccountId &&
                row.IsActive && row.IsPosting && !row.IsDeleted)
            .Select(row => new { row.Id, row.Code, row.Name })
            .SingleOrDefaultAsync(cancellationToken);
        if (account is null)
            return Result<MonetaryAccountRevaluationResponse>.Failure(AccountNotFound(request.AccountId));

        var party = await ResolvePartyAsync(request.PartyType, request.PartyId, request.Currency, cancellationToken);
        if (party.IsFailure)
            return Result<MonetaryAccountRevaluationResponse>.Failure(party.Errors);

        var fiscalYear = await dbContext.FiscalYears
            .AsNoTracking()
            .Where(year => year.CompanyId == companyId &&
                year.StartDate <= request.RevaluationDate &&
                year.EndDate >= request.RevaluationDate)
            .Select(year => new { year.Id, year.Status })
            .SingleOrDefaultAsync(cancellationToken);
        if (fiscalYear is null || fiscalYear.Status != FiscalYearStatus.Open)
            return Result<MonetaryAccountRevaluationResponse>.Failure(FiscalYearClosed());

        var existing = dbContext.MonetaryAccountRevaluations.Where(row =>
            row.CompanyId == companyId && row.AccountId == request.AccountId &&
            row.Currency == request.Currency && row.PartyType == request.PartyType &&
            row.PartyId == request.PartyId && !row.IsDeleted);
        if (await existing.AnyAsync(row => row.RevaluationDate == request.RevaluationDate, cancellationToken))
            return Result<MonetaryAccountRevaluationResponse>.Failure(Duplicate(request.RevaluationDate));
        if (await existing.AnyAsync(row => row.RevaluationDate > request.RevaluationDate, cancellationToken))
            return Result<MonetaryAccountRevaluationResponse>.Failure(Backdated(request.RevaluationDate));

        var lines = await dbContext.JournalEntryLines
            .AsNoTracking()
            .Where(line => line.CompanyId == companyId && line.AccountId == request.AccountId &&
                line.Currency != baseCurrency &&
                line.JournalEntry.EntryDate <= request.RevaluationDate &&
                (!request.PartyType.HasValue || line.PartyType == request.PartyType) &&
                (!request.PartyId.HasValue || line.PartyId == request.PartyId) &&
                !line.IsDeleted && !line.JournalEntry.IsDeleted &&
                line.JournalEntry.Status == JournalEntryStatus.Posted &&
                line.JournalEntry.ReversalOfEntryId == null &&
                line.JournalEntry.ReversedOn == null)
            .Select(line => new
            {
                line.Currency,
                line.TransactionDebit,
                line.TransactionCredit,
                line.Debit,
                line.Credit
            })
            .ToListAsync(cancellationToken);

        // Include all posted base-currency lines for carrying value, including
        // prior revaluation journals. The foreign quantity is restricted to the
        // selected currency and therefore cannot be changed by a revaluation.
        var carryingLines = await dbContext.JournalEntryLines
            .AsNoTracking()
            .Where(line => line.CompanyId == companyId && line.AccountId == request.AccountId &&
                line.JournalEntry.EntryDate <= request.RevaluationDate &&
                (line.Currency == request.Currency || line.Currency == baseCurrency) &&
                (!request.PartyType.HasValue || line.PartyType == request.PartyType) &&
                (!request.PartyId.HasValue || line.PartyId == request.PartyId) &&
                !line.IsDeleted && !line.JournalEntry.IsDeleted &&
                line.JournalEntry.Status == JournalEntryStatus.Posted &&
                line.JournalEntry.ReversalOfEntryId == null &&
                line.JournalEntry.ReversedOn == null)
            .Select(line => new { line.Debit, line.Credit })
            .ToListAsync(cancellationToken);

        var foreignAmount = ExchangeRateRules.RoundBaseAmount(lines
            .Where(line => line.Currency == request.Currency)
            .Sum(line => line.TransactionDebit - line.TransactionCredit));
        var carryingBaseAmount = ExchangeRateRules.RoundBaseAmount(carryingLines
            .Sum(line => line.Debit - line.Credit));
        if (foreignAmount < 0m || carryingBaseAmount < 0m)
            return Result<MonetaryAccountRevaluationResponse>.Failure(NegativeBalance());

        var targetBaseAmount = decimal.Round(
            ExchangeRateRules.ConvertToBase(foreignAmount, request.ClosingRate),
            4, MidpointRounding.AwayFromZero);
        var delta = decimal.Round(targetBaseAmount - carryingBaseAmount, 4, MidpointRounding.AwayFromZero);

        var row = new MonetaryAccountRevaluation
        {
            CompanyId = companyId,
            AccountId = request.AccountId,
            Currency = request.Currency,
            PartyType = request.PartyType,
            PartyId = request.PartyId,
            RevaluationDate = request.RevaluationDate,
            ClosingRate = ExchangeRateRules.RoundRate(request.ClosingRate),
            ForeignAmount = foreignAmount,
            CarryingBaseAmount = carryingBaseAmount,
            TargetBaseAmount = targetBaseAmount,
            DeltaBaseAmount = delta,
            CreatedOn = timeProvider.GetUtcNow().UtcDateTime
        };
        dbContext.MonetaryAccountRevaluations.Add(row);
        await dbContext.SaveChangesAsync(cancellationToken);

        int? journalEntryId = null;
        string? journalEntryNumber = null;
        if (delta != 0m)
        {
            var mappingType = delta > 0m ? AccountingMappingType.ExchangeGain : AccountingMappingType.ExchangeLoss;
            var differenceAccount = await accountMappingResolver.ResolveAsync(
                fiscalYear.Id, mappingType, cancellationToken: cancellationToken);
            if (differenceAccount.IsFailure)
                return Result<MonetaryAccountRevaluationResponse>.Failure(AccountMappingMissing());

            var amount = Math.Abs(delta);
            var targetLine = new JournalEntryLineRequest(
                AccountId: account.Id,
                Description: "إعادة تقييم حساب بعملة أجنبية",
                Debit: delta > 0m ? amount : 0m,
                Credit: delta < 0m ? amount : 0m,
                PartyType: request.PartyType,
                PartyId: request.PartyId,
                Currency: baseCurrency,
                ExchangeRate: 1m,
                TransactionDebit: delta > 0m ? amount : 0m,
                TransactionCredit: delta < 0m ? amount : 0m);
            var differenceLine = new JournalEntryLineRequest(
                AccountId: differenceAccount.Value,
                Description: delta > 0m ? "أرباح فروق إعادة تقييم العملة" : "خسائر فروق إعادة تقييم العملة",
                Debit: delta < 0m ? amount : 0m,
                Credit: delta > 0m ? amount : 0m,
                Currency: baseCurrency,
                ExchangeRate: 1m,
                TransactionDebit: delta < 0m ? amount : 0m,
                TransactionCredit: delta > 0m ? amount : 0m);
            var posting = await automaticPostingService.CreateOrUpdateAsync(
                new AutomaticJournalEntryRequest(
                    FiscalYearId: fiscalYear.Id,
                    EntryDate: request.RevaluationDate,
                    Description: $"إعادة تقييم {account.Name}",
                    SourceType: JournalEntrySourceType.MonetaryAccountRevaluation,
                    SourceId: row.Id,
                    SourceNumber: $"MAR-{row.Id}",
                    Lines: [targetLine, differenceLine]), cancellationToken);
            if (posting.IsFailure)
                return Result<MonetaryAccountRevaluationResponse>.Failure(posting.Errors);
            journalEntryId = posting.Value.JournalEntryId;
            journalEntryNumber = posting.Value.EntryNumber;
            row.JournalEntryId = journalEntryId;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        var response = new MonetaryAccountRevaluationResponse(
            Id: row.Id,
            AccountId: account.Id,
            AccountCode: account.Code,
            AccountName: account.Name,
            Currency: request.Currency,
            PartyType: request.PartyType,
            PartyId: request.PartyId,
            PartyCode: party.Value.Code,
            PartyName: party.Value.Name,
            RevaluationDate: row.RevaluationDate,
            ClosingRate: row.ClosingRate,
            ForeignAmount: row.ForeignAmount,
            CarryingBaseAmount: row.CarryingBaseAmount,
            TargetBaseAmount: row.TargetBaseAmount,
            DeltaBaseAmount: row.DeltaBaseAmount,
            JournalEntryId: journalEntryId,
            JournalEntryNumber: journalEntryNumber ?? string.Empty);
        return Result<MonetaryAccountRevaluationResponse>.Success(response);
    }

    public async Task<Result<IReadOnlyList<MonetaryAccountRevaluationResponse>>> GetAsync(
        int? accountId = null,
        JournalPartyType? partyType = null,
        int? partyId = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.MonetaryAccountRevaluations
            .AsNoTracking()
            .Where(row => row.CompanyId == companyId && !row.IsDeleted &&
                (!accountId.HasValue || row.AccountId == accountId.Value) &&
                (!partyType.HasValue || row.PartyType == partyType.Value) &&
                (!partyId.HasValue || row.PartyId == partyId.Value) &&
                (!fromDate.HasValue || row.RevaluationDate >= fromDate.Value) &&
                (!toDate.HasValue || row.RevaluationDate <= toDate.Value))
            .OrderByDescending(row => row.RevaluationDate)
            .ThenByDescending(row => row.Id)
            .Select(row => new MonetaryAccountRevaluationResponse(
                Id: row.Id,
                AccountId: row.AccountId,
                AccountCode: row.Account.Code,
                AccountName: row.Account.Name,
                Currency: row.Currency,
                PartyType: row.PartyType,
                PartyId: row.PartyId,
                PartyCode: null,
                PartyName: null,
                RevaluationDate: row.RevaluationDate,
                ClosingRate: row.ClosingRate,
                ForeignAmount: row.ForeignAmount,
                CarryingBaseAmount: row.CarryingBaseAmount,
                TargetBaseAmount: row.TargetBaseAmount,
                DeltaBaseAmount: row.DeltaBaseAmount,
                JournalEntryId: row.JournalEntryId,
                JournalEntryNumber: row.JournalEntry == null ? string.Empty : row.JournalEntry.EntryNumber))
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<MonetaryAccountRevaluationResponse>>.Success(
            await DecorateResponsesAsync(rows, cancellationToken));
    }

    private async Task<Result<(string? Code, string? Name)>> ResolvePartyAsync(
        JournalPartyType? partyType,
        int? partyId,
        CurrencyCode currency,
        CancellationToken cancellationToken)
    {
        if (!partyType.HasValue)
            return Result<(string? Code, string? Name)>.Success((null, null));
        if (!partyId.HasValue)
            return Result<(string? Code, string? Name)>.Failure(PartyRequired());
        if (partyType is JournalPartyType.Customer or JournalPartyType.Supplier)
        {
            var party = await dbContext.BusinessPartners.AsNoTracking()
                .Where(row => row.CompanyId == companyId && row.Id == partyId.Value && row.IsActive && !row.IsDeleted)
                .Select(row => new { row.Code, row.Name, row.Currency })
                .SingleOrDefaultAsync(cancellationToken);
            return party is null ? Result<(string? Code, string? Name)>.Failure(PartyNotFound())
                : party.Currency != currency ? Result<(string? Code, string? Name)>.Failure(PartyCurrencyMismatch())
                : Result<(string? Code, string? Name)>.Success((party.Code, party.Name));
        }
        if (partyType == JournalPartyType.Cashbox)
        {
            var cashbox = await dbContext.Cashboxes.AsNoTracking()
                .Where(row => row.CompanyId == companyId && row.Id == partyId.Value && row.IsActive && !row.IsDeleted)
                .Select(row => new { row.Code, row.Name, row.Currency })
                .SingleOrDefaultAsync(cancellationToken);
            return cashbox is null ? Result<(string? Code, string? Name)>.Failure(PartyNotFound())
                : cashbox.Currency != currency ? Result<(string? Code, string? Name)>.Failure(PartyCurrencyMismatch())
                : Result<(string? Code, string? Name)>.Success((cashbox.Code, cashbox.Name));
        }
        return Result<(string? Code, string? Name)>.Failure(UnsupportedParty());
    }

    private async Task<CurrencyCode> GetBaseCurrencyAsync(CancellationToken cancellationToken) =>
        await dbContext.Companies.AsNoTracking()
            .Where(company => company.Id == companyId)
            .Select(company => company.Settings == null ? CurrencyCode.EGP : company.Settings.BaseCurrency)
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<IReadOnlyList<MonetaryAccountRevaluationOption>> DecorateOptionsAsync(
        IReadOnlyList<MonetaryAccountRevaluationOption> options,
        CancellationToken cancellationToken)
    {
        var ids = options.Where(row => row.PartyId.HasValue).Select(row => row.PartyId!.Value).Distinct().ToArray();
        var partners = await dbContext.BusinessPartners.AsNoTracking()
            .Where(row => row.CompanyId == companyId && ids.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, row => (row.Code, row.Name), cancellationToken);
        var cashboxes = await dbContext.Cashboxes.AsNoTracking()
            .Where(row => row.CompanyId == companyId && ids.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, row => (row.Code, row.Name), cancellationToken);
        return options.Select(row =>
        {
            var values = row.PartyType == JournalPartyType.Cashbox && row.PartyId.HasValue && cashboxes.TryGetValue(row.PartyId.Value, out var cashbox)
                ? cashbox
                : row.PartyId.HasValue && partners.TryGetValue(row.PartyId.Value, out var partner) ? partner : (null, null);
            return row with { PartyCode = values.Item1, PartyName = values.Item2 };
        }).ToArray();
    }

    private async Task<IReadOnlyList<MonetaryAccountRevaluationResponse>> DecorateResponsesAsync(
        IReadOnlyList<MonetaryAccountRevaluationResponse> responses,
        CancellationToken cancellationToken)
    {
        var options = responses.Select(row => new MonetaryAccountRevaluationOption(
            AccountId: row.AccountId, AccountCode: row.AccountCode, AccountName: row.AccountName,
            Currency: row.Currency, PartyType: row.PartyType, PartyId: row.PartyId,
            PartyCode: row.PartyCode, PartyName: row.PartyName)).ToArray();
        var decorated = await DecorateOptionsAsync(options, cancellationToken);
        return responses.Select((row, index) => row with
        {
            PartyCode = decorated[index].PartyCode,
            PartyName = decorated[index].PartyName
        }).ToArray();
    }
}
