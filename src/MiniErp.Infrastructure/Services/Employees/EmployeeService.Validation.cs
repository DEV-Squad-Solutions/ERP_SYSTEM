using MiniErp.Application.Common.Results;
using MiniErp.Application.Features.Employees;
using MiniErp.Application.Features.Invoices;
using MiniErp.Domain.Entities.Invoicing;
using MiniErp.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace MiniErp.Infrastructure.Services.Employees
{
    public sealed partial class EmployeeService
    {
        private static Error? ValidateFilters(EmployeeFilterRequest filters)
        {
            if (filters.Code?.Trim().Length > 50)
                return Error.Validation(
                    "Employee.CodeTooLong",
                    "يجب ألا يزيد كود الموظف عن 50 حرفًا."
                    , nameof(filters.Code));
            if (filters.Name?.Trim().Length > 200)
                return Error.Validation(
                    "Employee.NameTooLong",
                    "يجب ألا يزيد اسم الموظف عن 200 حرف."
                    , nameof(filters.Name));
            if (filters.JobTitle?.Trim().Length > 200)
                return Error.Validation(
                    "Employee.JobTitleTooLong",
                    "يجب ألا يزيد المسمى الوظيفي للموظف عن 200 حرف."
                    , nameof(filters.JobTitle));
            if (filters.MinSalary < 0)
                return Error.Validation(
                    "Employee.MinSalaryNegative",
                    "يجب ألا يكون الحد الأدنى للراتب للموظف سالبًا."
                    , nameof(filters.MinSalary));
            if (filters.MaxSalary < 0)
                return Error.Validation(
                    "Employee.MaxSalaryNegative",
                    "يجب ألا يكون الحد الأعلى للراتب للموظف سالبًا."
                    , nameof(filters.MaxSalary));
            if (filters.Type is not null && !Enum.IsDefined(typeof(EmployeeType), filters.Type.Value))
                return Error.Validation(
                    "Employee.InvalidType",
                    "نوع الموظف المحدد غير صالح."
                    , nameof(filters.Type));
            return null;
        }

        private async Task<Error?> ValidateAddAsync(EmployeeCreateRequest request, CancellationToken cancellationToken)
        {  
            var PhoneNumberExists = await dbContext.Employees
                .AnyAsync(e => e.CompanyId == campanyId && e.PhoneNumber == request.PhoneNumber, cancellationToken);

            if (PhoneNumberExists)
            {
                return Error.Conflict(
                    "Employee.CodeAlreadyExists",
                    "رقم الهاتف للموظف موجود بالفعل لموظف آخر.",
                    nameof(request.PhoneNumber));
            }
            var EmailExists = await dbContext.Employees
                .AnyAsync(e => e.CompanyId == campanyId && e.Email == request.Email, cancellationToken);

            if (EmailExists)
            {
                return Error.Conflict(
                    "Employee.CodeAlreadyExists",
                    "البريد الإلكتروني للموظف موجود بالفعل لموظف آخر.",
                    nameof(request.Email));
            }

            return null;
        }

        private async Task<Error?> ValidateUpdateAsync(int id, EmployeeUpdateRequest request, CancellationToken cancellationToken)
        {
            var PhoneNumberExists = await dbContext.Employees
                .AnyAsync(e => e.CompanyId == campanyId && e.PhoneNumber == request.PhoneNumber && e.Id != id, cancellationToken);

            if (PhoneNumberExists)
            {
                return Error.Conflict(
                    "Employee.CodeAlreadyExists",
                    "رقم الهاتف للموظف موجود بالفعل لموظف آخر.",
                    nameof(request.PhoneNumber));
            }
            var EmailExists = await dbContext.Employees
                .AnyAsync(e => e.CompanyId == campanyId && e.Email == request.Email && e.Id != id, cancellationToken);

            if (EmailExists)
            {
                return Error.Conflict(
                    "Employee.CodeAlreadyExists",
                    "البريد الإلكتروني للموظف موجود بالفعل لموظف آخر.",
                    nameof(request.Email));
            }


            return null;
        }

        private static Error InvalidFilter(string target, string description) =>
            Error.Validation(
                "Employees.InvalidFilter",
                description,
                target);
    }
}
