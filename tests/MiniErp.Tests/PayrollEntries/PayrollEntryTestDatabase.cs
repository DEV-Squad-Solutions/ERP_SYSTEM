using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Features.CashVouchers;
using MiniErp.Application.Features.EmployeeMovements;
using MiniErp.Application.Features.EmployeeOpeningBalances;
using MiniErp.Application.Features.ExchangeRates;
using MiniErp.Application.Features.PayrollEntries;
using MiniErp.Application.Features.ProfitabilityReports;
using MiniErp.Application.Features.Statements;
using MiniErp.Domain.Entities.CashManagement;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Services.CashVouchers;
using MiniErp.Infrastructure.Services.EmployeeMovements;
using MiniErp.Infrastructure.Services.EmployeeOpeningBalances;
using MiniErp.Infrastructure.Services.ExchangeRates;
using MiniErp.Infrastructure.Services.Pagination;
using MiniErp.Infrastructure.Services.PayrollEntries;
using MiniErp.Infrastructure.Services.Statements;
using System;
using System.Threading.Tasks;

namespace MiniErp.Tests.PayrollEntries;

public sealed class PayrollEntryTestDatabase : IAsyncDisposable
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

    public static async Task<PayrollEntryTestDatabase> CreateAsync(int companyId)
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
        services.AddScoped<IEmployeeMovementService, EmployeeMovementService>();
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
        var company = new Company
        {
            Id = companyId,
            Name = "Test Company",
            Address = "123 Test St",
            CommercialRegister = $"CR-{companyId}",
            TaxNumber = $"TAX-{companyId}",
            ManagerName = "Manager"
        };
        typeof(Company).GetProperty("RowVersion")?.SetValue(company, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        context.Companies.Add(company);

        var cashbox = new Cashbox
        {
            Id = 1,
            CompanyId = companyId,
            Code = "CB01",
            Name = "Main Cashbox",
            Currency = CurrencyCode.EGP,
            OpeningBalance = 100000.0m,
            IsActive = true
        };
        cashbox.ApplyOpeningExchangeRate(new DateOnly(2026, 1, 1), null, 1.0m);
        context.Cashboxes.Add(cashbox);

        var employees = new[]
        {
            new Employee
            {
                Id = 1,
                CompanyId = companyId,
                Code = "EMP001",
                Name = "Monthly Employee",
                Email = "emp1@test.com",
                PhoneNumber = "0123456789",
                Type = EmployeeType.Monthly,
                MonthlySalary = 6000m,
                RequiredWorkingDaysPerMonth = 30,
                IsActive = true
            },
            new Employee
            {
                Id = 2,
                CompanyId = companyId,
                Code = "EMP002",
                Name = "Daily Employee",
                Email = "emp2@test.com",
                PhoneNumber = "0987654321",
                Type = EmployeeType.Daily,
                DailySalary = 200m,
                IsActive = true
            },
            new Employee
            {
                Id = 3,
                CompanyId = companyId,
                Code = "EMP003",
                Name = "Third Employee",
                Email = "emp3@test.com",
                PhoneNumber = "0112233445",
                Type = EmployeeType.Monthly,
                MonthlySalary = 9000m,
                RequiredWorkingDaysPerMonth = 30,
                IsActive = true
            }
        };
        context.Employees.AddRange(employees);
        await context.SaveChangesAsync();
    }

    public IPayrollEntryService CreatePayrollService()
    {
        return scope.ServiceProvider.GetRequiredService<IPayrollEntryService>();
    }

    public IEmployeeOpeningBalanceService CreateOpeningBalanceService()
    {
        return scope.ServiceProvider.GetRequiredService<IEmployeeOpeningBalanceService>();
    }

    public IEmployeeMovementService CreateMovementService()
    {
        return scope.ServiceProvider.GetRequiredService<IEmployeeMovementService>();
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
