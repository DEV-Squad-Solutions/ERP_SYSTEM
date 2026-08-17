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
            if (filters.    MinSalary < 0)
                return Error.Validation(
                    "Employee.MinSalaryNegative",
                    "يجب ألا يكون الحد الأدنى للراتب للموظف سالبًا."
                    , nameof(filters.MinSalary));
            if (filters.MaxSalary < 0)
                return Error.Validation(
                    "Employee.MaxSalaryNegative",
                    "يجب ألا يكون الحد الأعلى للراتب للموظف سالبًا."
                    , nameof(filters.MaxSalary));
            if (filters.EmployeeType is not null && !Enum.IsDefined(typeof(EmployeeType), filters.EmployeeType.Value))
                return Error.Validation(
                    "Employee.InvalidType",
                    "نوع الموظف المحدد غير صالح."
                    , nameof(filters.EmployeeType));
            return null;
        }

        private async Task<Error?> ValidateAddAsync(EmployeeCreateRequest request, CancellationToken cancellationToken)
        {
            if(request==null)
                return Error.Validation(
                    "Employee.InvalidRequest",
                    "طلب إنشاء موظف غير صالح."
                    , nameof(request));
            if(string.IsNullOrWhiteSpace(request.Name))
                return Error.Validation(
                    "Employee.InvalidName",
                    "اسم الموظف غير صالح."
                    , nameof(request.Name));
            if (!Enum.IsDefined(typeof(EmployeeType), request.Type))
                return Error.Validation(
                    "Employee.InvalidType",
                    "نوع الموظف المحدد غير صالح."
                    , nameof(request.Type));
            if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                var phoneExists = await dbContext.Employees
                    .AnyAsync(e => e.CompanyId == campanyId && e.PhoneNumber == request.PhoneNumber.Trim(), cancellationToken);
                if (phoneExists)
                    return Error.Conflict(
                        "Employee.PhoneAlreadyExists",
                        "رقم الهاتف مستخدم بالفعل لموظف آخر.",
                        nameof(request.PhoneNumber));
            }

            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                var emailExists = await dbContext.Employees
                    .AnyAsync(e => e.CompanyId == campanyId && e.Email == request.Email.Trim().ToLower(), cancellationToken);
                if (emailExists)
                    return Error.Conflict(
                        "Employee.EmailAlreadyExists",
                        "البريد الإلكتروني مستخدم بالفعل لموظف آخر.",
                        nameof(request.Email));
            }

            return null;
        }

        private async Task<Error?> ValidateUpdateAsync(int id, EmployeeUpdateRequest request, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                var phoneExists = await dbContext.Employees
                    .AnyAsync(e => e.CompanyId == campanyId && e.PhoneNumber == request.PhoneNumber.Trim() && e.Id != id, cancellationToken);
                if (phoneExists)
                    return Error.Conflict(
                        "Employee.PhoneAlreadyExists",
                        "رقم الهاتف مستخدم بالفعل لموظف آخر.",
                        nameof(request.PhoneNumber));
            }

            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                var emailExists = await dbContext.Employees
                    .AnyAsync(e => e.CompanyId == campanyId && e.Email == request.Email.Trim().ToLower() && e.Id != id, cancellationToken);
                if (emailExists)
                    return Error.Conflict(
                        "Employee.EmailAlreadyExists",
                        "البريد الإلكتروني مستخدم بالفعل لموظف آخر.",
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
