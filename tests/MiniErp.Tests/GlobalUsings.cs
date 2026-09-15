global using Xunit;

namespace MiniErp.Tests.Employees;

public sealed class EmployeeWorkplaceTests
{
    [Fact]
    public void Employee_UpdateWorkPlace_ShouldSetPropertiesProperly()
    {
        var emp = new MiniErp.Domain.Entities.Employees.Employee
        {
            Id = 1,
            CompanyId = 1,
            Name = "Ahmed",
            Type = MiniErp.Domain.Enums.EmployeeType.Monthly,
            MonthlySalary = 5000m,
            IsActive = true
        };

        emp.UpdateWorkPlace(MiniErp.Domain.Enums.WorkPlaceStatus.OutCompany, "  Branch 5  ");

        Assert.Equal(MiniErp.Domain.Enums.WorkPlaceStatus.OutCompany, emp.WorkPlaceStatus);
        Assert.Equal("Branch 5", emp.PlaceName);

        emp.UpdateWorkPlace(MiniErp.Domain.Enums.WorkPlaceStatus.InCompany, "   ");
        Assert.Equal(MiniErp.Domain.Enums.WorkPlaceStatus.InCompany, emp.WorkPlaceStatus);
        Assert.Null(emp.PlaceName);
    }

    [Fact]
    public void Employee_SetIsActive_ShouldToggleStatus()
    {
        var emp = new MiniErp.Domain.Entities.Employees.Employee
        {
            Id = 1,
            CompanyId = 1,
            Name = "Ahmed",
            Type = MiniErp.Domain.Enums.EmployeeType.Monthly,
            IsActive = true
        };

        emp.SetIsActive(false);
        Assert.False(emp.IsActive);

        emp.SetIsActive(true);
        Assert.True(emp.IsActive);
    }

    [Fact]
    public void Employee_SetRequiredWorkingDays_ShouldThrow_WhenEmployeeIsDaily()
    {
        var emp = new MiniErp.Domain.Entities.Employees.Employee
        {
            Id = 1,
            CompanyId = 1,
            Name = "Daily Guy",
            Type = MiniErp.Domain.Enums.EmployeeType.Daily,
            DailySalary = 200m
        };

        Assert.Throws<InvalidOperationException>(() => emp.SetRequiredWorkingDays(26));
    }

    [Fact]
    public void Employee_SetRequiredWorkingDays_ShouldSucceed_WhenEmployeeIsMonthly()
    {
        var emp = new MiniErp.Domain.Entities.Employees.Employee
        {
            Id = 1,
            CompanyId = 1,
            Name = "Monthly Guy",
            Type = MiniErp.Domain.Enums.EmployeeType.Monthly,
            MonthlySalary = 6000m
        };

        emp.SetRequiredWorkingDays(28);
        Assert.Equal(28, emp.RequiredWorkingDaysPerMonth);
    }

    [Fact]
    public void Employee_UpdateLastDayOfReceivingSalary_ShouldUpdateValue()
    {
        var emp = new MiniErp.Domain.Entities.Employees.Employee
        {
            Id = 1,
            CompanyId = 1,
            Name = "Worker",
            Type = MiniErp.Domain.Enums.EmployeeType.Monthly
        };

        Assert.Null(emp.LastDayOfReceivingSalary);

        var date = new DateOnly(2026, 8, 31);
        emp.UpdateLastDayOfReceivingSalary(date);

        Assert.Equal(date, emp.LastDayOfReceivingSalary);
    }

    [Fact]
    public void EmployeeCreateRequestValidator_ShouldFail_WhenDailyHasRequiredWorkingDays()
    {
        var validator = new MiniErp.Application.Features.Employees.EmployeeCreateRequestValidator();
        var request = new MiniErp.Application.Features.Employees.EmployeeCreateRequest(
            Name: "Daily Guy",
            JobTitle: null,
            PhoneNumber: null,
            Email: null,
            Address: null,
            Type: MiniErp.Domain.Enums.EmployeeType.Daily,
            Salary: 200m,
            RequiredWorkingDaysPerMonth: 26,
            WorkPlaceStatus: MiniErp.Domain.Enums.WorkPlaceStatus.InCompany);

        var validationResult = validator.Validate(request);

        Assert.False(validationResult.IsValid);
        Assert.Contains(validationResult.Errors, e => e.PropertyName == nameof(request.RequiredWorkingDaysPerMonth));
    }

    [Fact]
    public void EmployeeCreateRequestValidator_ShouldFail_WhenWorkPlaceStatusIsInvalid()
    {
        var validator = new MiniErp.Application.Features.Employees.EmployeeCreateRequestValidator();
        var request = new MiniErp.Application.Features.Employees.EmployeeCreateRequest(
            Name: "Invalid Workplace Guy",
            JobTitle: null,
            PhoneNumber: null,
            Email: null,
            Address: null,
            Type: MiniErp.Domain.Enums.EmployeeType.Monthly,
            Salary: 5000m,
            RequiredWorkingDaysPerMonth: 26,
            WorkPlaceStatus: (MiniErp.Domain.Enums.WorkPlaceStatus)99);

        var validationResult = validator.Validate(request);

        Assert.False(validationResult.IsValid);
        Assert.Contains(validationResult.Errors, e => e.PropertyName == nameof(request.WorkPlaceStatus));
    }

    [Fact]
    public void EmployeeUpdateRequestValidator_ShouldFail_WhenDailyHasRequiredWorkingDays()
    {
        var validator = new MiniErp.Application.Features.Employees.EmployeeUpdateRequestValidator();
        var request = new MiniErp.Application.Features.Employees.EmployeeUpdateRequest(
            CompanyId: 1,
            Name: "Daily Guy",
            JobTitle: null,
            PhoneNumber: null,
            Email: null,
            Address: null,
            Type: MiniErp.Domain.Enums.EmployeeType.Daily,
            Salary: 200m,
            RequiredWorkingDaysPerMonth: 26);

        var validationResult = validator.Validate(request);

        Assert.False(validationResult.IsValid);
        Assert.Contains(validationResult.Errors, e => e.PropertyName == nameof(request.RequiredWorkingDaysPerMonth));
    }

    [Fact]
    public void OutCompanyPayrollEntryRequestValidator_ShouldValidateCorrectly()
    {
        var validator = new MiniErp.Application.Features.PayrollEntries.OutCompanyPayrollEntryRequestValidator();

        var invalidRequest = new MiniErp.Application.Features.PayrollEntries.OutCompanyPayrollEntryRequest(
            EmployeeId: 0,
            StartDate: new DateOnly(2026, 8, 10),
            EndDate: new DateOnly(2026, 8, 1),
            PresentDays: -1,
            WorkedDaysByDayUnit: -5m);

        var result = validator.Validate(invalidRequest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(invalidRequest.EmployeeId));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(invalidRequest.PresentDays));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(invalidRequest.WorkedDaysByDayUnit));

        var validRequest = new MiniErp.Application.Features.PayrollEntries.OutCompanyPayrollEntryRequest(
            EmployeeId: 5,
            StartDate: new DateOnly(2026, 8, 1),
            EndDate: new DateOnly(2026, 8, 10),
            PresentDays: 10,
            WorkedDaysByDayUnit: 10m,
            OvertimeByDayUnit: 2m,
            DeductionByDayUnit: 1m,
            Bonus: 100m,
            Deduction: 50m);

        var validResult = validator.Validate(validRequest);
        Assert.True(validResult.IsValid);
    }
}
