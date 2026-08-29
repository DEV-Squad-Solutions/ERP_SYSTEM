using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Features.CashVouchers;
using MiniErp.Application.Features.EmployeeOpeningBalances;
using MiniErp.Application.Features.ExchangeRates;
using MiniErp.Application.Features.PayrollEntries;
using MiniErp.Application.Features.ProfitabilityReports;
using MiniErp.Application.Features.Statements;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Services.CashVouchers;
using MiniErp.Infrastructure.Services.EmployeeOpeningBalances;
using MiniErp.Infrastructure.Services.ExchangeRates;
using MiniErp.Infrastructure.Services.Pagination;
using MiniErp.Infrastructure.Services.PayrollEntries;
using MiniErp.Infrastructure.Services.Statements;
using System;
using System.Threading.Tasks;

namespace MiniErp.Tests.PayrollEntries;

internal sealed class PayrollEntryTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection connection;
    private readonly ServiceProvider serviceProvider;
    private readonly AsyncServiceScope scope;

    public ApplicationDbContext Context { get; }

    private PayrollEntryTestDatabase(
        SqliteConnection connection,
        ServiceProvider serviceProvider,
        AsyncServiceScope scope,
        ApplicationDbContext context)
    {
        this.connection = connection;
        this.serviceProvider = serviceProvider;
        this.scope = scope;
        Context = context;
    }

    public static async Task<PayrollEntryTestDatabase> CreateAsync(int companyId = 1)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlite(connection));

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IPaginationService, PaginationService>();
        services.AddScoped<IExchangeRateResolver, ExchangeRateResolver>();
        services.AddScoped<ICashVoucherService, CashVoucherService>();
        services.AddScoped<IEmployeeOpeningBalanceService, EmployeeOpeningBalanceService>();
        services.AddScoped<IPayrollEntryService, PayrollEntryService>();
        services.AddScoped<IFinancialStatementService, FinancialStatementService>();
        services.AddSingleton<ICurrentCompanyContext>(new TestCurrentCompanyContext(companyId));

        var serviceProvider = services.BuildServiceProvider();
        var scope = serviceProvider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await context.Database.EnsureCreatedAsync();
        await SeedRequiredDataAsync(context, companyId);

        return new PayrollEntryTestDatabase(
            connection,
            serviceProvider,
            scope,
            context);
    }

    private static async Task SeedRequiredDataAsync(ApplicationDbContext context, int companyId)
    {
        await context.Database.ExecuteSqlRawAsync($@"
            INSERT INTO Companies (Id, Code, Name, BaseCurrency, IsActive, CreatedOn)
            VALUES ({companyId}, 'COMP01', 'Test Company', 1, 1, '2026-01-01');

            INSERT INTO Cashboxes (Id, CompanyId, Code, Name, Currency, IsActive, OpeningBalance, BaseOpeningBalance, OpeningBalanceDate, OpeningExchangeRate, CreatedOn)
            VALUES (1, {companyId}, 'CB01', 'Main Cashbox', 1, 1, 100000.0, 100000.0, '2026-01-01', 1.0, '2026-01-01');

            INSERT INTO CashMovementTypes (Id, CompanyId, Code, Name, Classification, AllowedDirection, IsActive, CreatedOn)
            VALUES 
                (1, {companyId}, 'SAL_PAY', 'Payroll Salary Payment', 1, 2, 1, '2026-01-01'),
                (2, {companyId}, 'EMP_DED', 'Employee Cash Deduction', 1, 1, 1, '2026-01-01');

            INSERT INTO Employees (Id, CompanyId, Code, Name, Email, PhoneNumber, Type, MonthlySalary, DailySalary, RequiredWorkingDaysPerMonth, IsActive)
            VALUES 
                (1, {companyId}, 'EMP001', 'Monthly Employee', 'emp1@test.com', '0123456789', 0, 6000, NULL, 30, 1),
                (2, {companyId}, 'EMP002', 'Daily Employee', 'emp2@test.com', '0987654321', 1, NULL, 200, NULL, 1),
                (3, {companyId}, 'EMP003', 'Third Employee', 'emp3@test.com', '0112233445', 0, 9000, NULL, 30, 1);
        ");
    }

    public IPayrollEntryService CreatePayrollService()
    {
        return scope.ServiceProvider.GetRequiredService<IPayrollEntryService>();
    }

    public IEmployeeOpeningBalanceService CreateOpeningBalanceService()
    {
        return scope.ServiceProvider.GetRequiredService<IEmployeeOpeningBalanceService>();
    }

    public IFinancialStatementService CreateStatementService()
    {
        return scope.ServiceProvider.GetRequiredService<IFinancialStatementService>();
    }

    public async ValueTask DisposeAsync()
    {
        await scope.DisposeAsync();
        await serviceProvider.DisposeAsync();
        await connection.CloseAsync();
        await connection.DisposeAsync();
    }

    private sealed record TestCurrentCompanyContext(int CompanyId) : ICurrentCompanyContext;
}
