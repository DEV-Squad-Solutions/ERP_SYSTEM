using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace MiniErp.Application.Features.Employees
{
    public sealed class EmployeeRequestValidator : AbstractValidator<EmployeeRequest>
    {
        public EmployeeRequestValidator() {
            RuleFor(x => x.Name)
                .MaximumLength(200);
            RuleFor(x => x.JobTitle)
                .MaximumLength(200);
            RuleFor(x => x.PhoneNumber)
                .MaximumLength(50);
            RuleFor(x => x.Email)
                .MaximumLength(256);
            RuleFor(x => x.Address)
                .MaximumLength(500);
            RuleFor(x => x.Type)
                .IsInEnum();
            RuleFor(x => x.Salary)
                .PrecisionScale(18, 2, ignoreTrailingZeros: true);
            RuleFor(x => x.RequiredWorkingDaysPerMonth)
                .GreaterThan(0);

                
        }
    }
}
