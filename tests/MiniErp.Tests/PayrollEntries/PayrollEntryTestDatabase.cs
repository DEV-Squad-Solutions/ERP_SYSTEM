using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Features.EmployeeTransactions;
using MiniErp.Application.Features.PayrollEntries;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Services.EmployeeTransactions;
using MiniErp.Infrastructure.Services.Pagination;
using MiniErp.Infrastructure.Services.PayrollEntries;
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
        services.AddScoped<IEmployeeTransactionService, EmployeeTransactionService>();
        services.AddScoped<IPayrollEntryService, PayrollEntryService>();
        services.AddSingleton<ICurrentCompanyContext>(new TestCurrentCompanyContext(companyId));

        var serviceProvider = services.BuildServiceProvider();
        var scope = serviceProvider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await CreateSchemaAsync(context);
        await SeedDataAsync(context, companyId);

        return new PayrollEntryTestDatabase(connection, serviceProvider, scope, context);
    }

    private static async Task CreateSchemaAsync(ApplicationDbContext context)
    {
        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE Companies (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Address TEXT NOT NULL,
                CommercialRegister TEXT NOT NULL,
                TaxNumber TEXT NOT NULL,
                ManagerName TEXT NOT NULL,
                RowVersion BLOB NOT NULL DEFAULT (randomblob(8)),
                CreatedById TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
                CreatedOn TEXT NOT NULL DEFAULT '2026-01-01',
                CreatedByPc TEXT NOT NULL DEFAULT 'TEST',
                UpdatedById TEXT NULL,
                UpdatedOn TEXT NULL,
                UpdatedByPc TEXT NULL,
                DeletedById TEXT NULL,
                DeletedOn TEXT NULL,
                DeletedByPc TEXT NULL,
                IsDeleted INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE Employees (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CompanyId INTEGER NOT NULL,
                Code TEXT NOT NULL,
                Name TEXT NOT NULL,
                JobTitle TEXT NULL,
                PhoneNumber TEXT NULL,
                Email TEXT NULL,
                Address TEXT NULL,
                Type INTEGER NOT NULL,
                DailySalary NUMERIC NULL,
                MonthlySalary NUMERIC NULL,
                RequiredWorkingDaysPerMonth INTEGER NULL,
                LastDayOfReceivingSalary TEXT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1,
                CreatedById TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
                CreatedOn TEXT NOT NULL DEFAULT '2026-01-01',
                CreatedByPc TEXT NOT NULL DEFAULT 'TEST',
                UpdatedById TEXT NULL,
                UpdatedOn TEXT NULL,
                UpdatedByPc TEXT NULL,
                DeletedById TEXT NULL,
                DeletedOn TEXT NULL,
                DeletedByPc TEXT NULL,
                IsDeleted INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE EmployeeAttendances (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CompanyId INTEGER NOT NULL,
                EmployeeId INTEGER NOT NULL,
                Status INTEGER NOT NULL,
                WorkDate TEXT NOT NULL,
                CheckIn TEXT NULL,
                CheckOut TEXT NULL,
                WorkHours TEXT NULL,
                WorkDayRatio INTEGER NOT NULL DEFAULT 1,
                WorkOverTimeRatio INTEGER NULL,
                WorkDaysDeductionRatio INTEGER NULL,
                WorkLocation TEXT NULL,
                Notes TEXT NULL,
                CreatedById TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
                CreatedOn TEXT NOT NULL DEFAULT '2026-01-01',
                CreatedByPc TEXT NOT NULL DEFAULT 'TEST',
                UpdatedById TEXT NULL,
                UpdatedOn TEXT NULL,
                UpdatedByPc TEXT NULL,
                DeletedById TEXT NULL,
                DeletedOn TEXT NULL,
                DeletedByPc TEXT NULL,
                IsDeleted INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE PayrollEntries (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CompanyId INTEGER NOT NULL,
                EmployeeId INTEGER NOT NULL,
                EmployeeCode TEXT NOT NULL,
                EmployeeName TEXT NOT NULL,
                EmployeeType INTEGER NOT NULL,
                StartDate TEXT NOT NULL,
                EndDate TEXT NOT NULL,
                PresentDays INTEGER NOT NULL DEFAULT 0,
                AbsentDays INTEGER NOT NULL DEFAULT 0,
                WorkedDaysbydayunit NUMERIC NOT NULL DEFAULT 0,
                Overtimebydayunit NUMERIC NULL,
                Deductionbydayunit NUMERIC NULL,
                RequiredWorkingDays NUMERIC NULL,
                SalaryPerDay NUMERIC NULL,
                GrossSalary NUMERIC NOT NULL DEFAULT 0,
                CalculatedSalary NUMERIC NOT NULL DEFAULT 0,
                NetSalary NUMERIC NOT NULL DEFAULT 0,
                Bonus NUMERIC NULL,
                Deduction NUMERIC NULL,
                CashboxId INTEGER NULL,
                CashVoucherId INTEGER NULL,
                IsTakeSalary INTEGER NOT NULL DEFAULT 0,
                CreatedById TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
                CreatedOn TEXT NOT NULL DEFAULT '2026-01-01',
                CreatedByPc TEXT NOT NULL DEFAULT 'TEST',
                UpdatedById TEXT NULL,
                UpdatedOn TEXT NULL,
                UpdatedByPc TEXT NULL,
                DeletedById TEXT NULL,
                DeletedOn TEXT NULL,
                DeletedByPc TEXT NULL,
                IsDeleted INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE EmployeeTransactions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CompanyId INTEGER NOT NULL,
                EmployeeId INTEGER NOT NULL,
                Type INTEGER NOT NULL,
                Amount NUMERIC NOT NULL,
                TransactionDate TEXT NOT NULL,
                Notes TEXT NULL,
                RunningBalance NUMERIC NOT NULL DEFAULT 0,
                SourceType INTEGER NOT NULL DEFAULT 1,
                SourceId INTEGER NULL,
                CashVoucherId INTEGER NULL,
                CreatedById TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
                CreatedOn TEXT NOT NULL DEFAULT '2026-01-01',
                CreatedByPc TEXT NOT NULL DEFAULT 'TEST',
                UpdatedById TEXT NULL,
                UpdatedOn TEXT NULL,
                UpdatedByPc TEXT NULL,
                DeletedById TEXT NULL,
                DeletedOn TEXT NULL,
                DeletedByPc TEXT NULL,
                IsDeleted INTEGER NOT NULL DEFAULT 0
            );
            """);
    }

    private static async Task SeedDataAsync(ApplicationDbContext context, int companyId)
    {
        var company = new Company
        {
            Id = companyId,
            Name = "Test Company",
            Address = "123 Test St",
            CommercialRegister = "12345",
            TaxNumber = "67890",
            ManagerName = "Test Manager"
        };
        context.Companies.Add(company);

        var emp1 = new Employee
        {
            Id = 1,
            CompanyId = companyId,
            Code = "EMP001",
            Name = "Monthly Employee",
            Email = "emp1@test.com",
            PhoneNumber = "0123456789",
            Type = EmployeeType.Monthly,
            MonthlySalary = 6000m,
            DailySalary = null,
            RequiredWorkingDaysPerMonth = 30,
            LastDayOfReceivingSalary = null,
            IsActive = true
        };

        var emp2 = new Employee
        {
            Id = 2,
            CompanyId = companyId,
            Code = "EMP002",
            Name = "Daily Employee",
            Email = "emp2@test.com",
            PhoneNumber = "0987654321",
            Type = EmployeeType.Daily,
            DailySalary = 200m,
            MonthlySalary = null,
            LastDayOfReceivingSalary = null,
            IsActive = true
        };

        var emp3 = new Employee
        {
            Id = 3,
            CompanyId = companyId,
            Code = "EMP003",
            Name = "Third Employee",
            Email = "emp3@test.com",
            PhoneNumber = "0112233445",
            Type = EmployeeType.Monthly,
            MonthlySalary = 9000m,
            DailySalary = null,
            RequiredWorkingDaysPerMonth = 30,
            LastDayOfReceivingSalary = null,
            IsActive = true
        };

        context.Employees.AddRange(emp1, emp2, emp3);
        await context.SaveChangesAsync();
    }

    public IPayrollEntryService CreatePayrollService()
    {
        return scope.ServiceProvider.GetRequiredService<IPayrollEntryService>();
    }

    public IEmployeeTransactionService CreateTransactionService()
    {
        return scope.ServiceProvider.GetRequiredService<IEmployeeTransactionService>();
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
