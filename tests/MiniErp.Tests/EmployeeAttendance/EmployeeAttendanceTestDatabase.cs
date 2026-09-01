using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniErp.Application.Common.Abstractions;
using MiniErp.Application.Features.EmployeeAttendance;
using MiniErp.Domain.Entities.Companies;
using MiniErp.Domain.Entities.Employees;
using MiniErp.Domain.Enums;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Services.EmployeeAttendance;
using MiniErp.Infrastructure.Services.Pagination;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MiniErp.Tests.EmployeeAttendance;

internal sealed class EmployeeAttendanceTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection connection;
    private readonly ServiceProvider serviceProvider;
    private readonly AsyncServiceScope scope;

    public ApplicationDbContext Context { get; }

    private EmployeeAttendanceTestDatabase(
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

    public static async Task<EmployeeAttendanceTestDatabase> CreateAsync(int companyId = 1)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlite(connection));

        services.AddScoped<IPaginationService, PaginationService>();
        services.AddScoped<IEmployeeAttendanceService, EmployeeAttendanceService>();
        services.AddSingleton<ICurrentCompanyContext>(new TestCurrentCompanyContext(companyId));

        var serviceProvider = services.BuildServiceProvider();
        var scope = serviceProvider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await context.Database.EnsureCreatedAsync();
        await context.Database.ExecuteSqlRawAsync("DROP TABLE IF EXISTS Companies; CREATE TABLE Companies (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, Address TEXT NOT NULL, CommercialRegister TEXT NOT NULL, TaxNumber TEXT NOT NULL, ManagerName TEXT NOT NULL, RowVersion BLOB NOT NULL DEFAULT (X'0102030405060708'), CreatedById TEXT NOT NULL DEFAULT '', CreatedOn TEXT NOT NULL DEFAULT '2026-01-01', CreatedByPc TEXT NOT NULL DEFAULT '', UpdatedById TEXT NULL, UpdatedOn TEXT NULL, UpdatedByPc TEXT NULL, DeletedById TEXT NULL, DeletedOn TEXT NULL, DeletedByPc TEXT NULL, IsDeleted INTEGER NOT NULL DEFAULT 0);");
        await SeedDataAsync(context, companyId);

        return new EmployeeAttendanceTestDatabase(connection, serviceProvider, scope, context);
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
        context.Entry(company).Property(c => c.RowVersion).CurrentValue = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        var emp1 = new Employee
        {
            Id = 1,
            CompanyId = companyId,
            Name = "Employee One",
            Email = "emp1@test.com",
            PhoneNumber = "0123456789",
            Type = EmployeeType.Monthly,
            MonthlySalary = 5000,
            DailySalary = null,
            RequiredWorkingDaysPerMonth = 26,
            IsActive = true
        };
        var emp2 = new Employee
        {
            Id = 2,
            CompanyId = companyId,
            Name = "Employee Two",
            Email = "emp2@test.com",
            PhoneNumber = "0987654321",
            Type = EmployeeType.Daily,
            DailySalary = 200,
            MonthlySalary = null,
            IsActive = true
        };

        context.Employees.AddRange(emp1, emp2);
        await context.SaveChangesAsync();
    }

    public IEmployeeAttendanceService CreateService()
    {
        return scope.ServiceProvider.GetRequiredService<IEmployeeAttendanceService>();
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
