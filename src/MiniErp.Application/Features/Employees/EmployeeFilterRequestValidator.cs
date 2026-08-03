using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace MiniErp.Application.Features.Employees
{
    public sealed class EmployeeFilterRequestValidator: AbstractValidator<EmployeeFilterRequest>
    {
        public EmployeeFilterRequestValidator()
        {
            RuleFor(x => x.Search)
                .MaximumLength(200);
            RuleFor(x => x.Name)
                .MaximumLength(200);
            RuleFor(x => x.Code)
                .MaximumLength(50);
            RuleFor(x => x.JobTitle)
                .MaximumLength(50);
            RuleFor(x => x.MinSalary)
                .GreaterThanOrEqualTo(0)
                .LessThanOrEqualTo(x => x.MaxSalary ?? decimal.MaxValue);
            RuleFor(x => x.MaxSalary)
                .GreaterThanOrEqualTo(0)
                .PrecisionScale(18, 2, ignoreTrailingZeros: true)
                .GreaterThanOrEqualTo(x => x.MinSalary ?? decimal.MinValue);
            RuleFor(x => x.Type)
                .IsInEnum()
                .When(x => x.Type.HasValue);
        }
    }
}
