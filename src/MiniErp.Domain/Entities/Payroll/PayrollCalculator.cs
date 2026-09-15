using MiniErp.Domain.Entities.Employees;
using MiniErp.Domain.Enums;

namespace MiniErp.Domain.Entities.Payroll;

public static class PayrollCalculator
{
    public static (decimal GrossSalary, decimal CalculatedSalary, decimal NetSalary, decimal? SalaryPerDay) Calculate(
        Employee employee,
        decimal workedUnits,
        decimal? bonus = null,
        decimal? deduction = null)
    {
        decimal grossSalary;
        decimal calculatedSalary;
        decimal? salaryPerDay;

        if (employee.Type == EmployeeType.Monthly && employee.MonthlySalary.HasValue)
        {
            grossSalary = employee.MonthlySalary.Value;
            if (employee.RequiredWorkingDaysPerMonth is > 0)
            {
                salaryPerDay = grossSalary / employee.RequiredWorkingDaysPerMonth.Value;
                calculatedSalary = salaryPerDay.Value * workedUnits;
            }
            else
            {
                salaryPerDay = grossSalary;
                calculatedSalary = grossSalary;
            }
        }
        else if (employee.Type == EmployeeType.Daily && employee.DailySalary.HasValue)
        {
            grossSalary = employee.DailySalary.Value;
            salaryPerDay = grossSalary;
            calculatedSalary = grossSalary * workedUnits;
        }
        else
        {
            return (GrossSalary: -1m, CalculatedSalary: -1m, NetSalary: -1m, SalaryPerDay: null);
        }

        var netSalary = calculatedSalary + (bonus ?? 0m) - (deduction ?? 0m);

        return (
            GrossSalary: grossSalary,
            CalculatedSalary: calculatedSalary,
            NetSalary: netSalary,
            SalaryPerDay: salaryPerDay);
    }
}
