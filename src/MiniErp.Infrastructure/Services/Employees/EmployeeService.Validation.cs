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
            if (!string.IsNullOrWhiteSpace(filters.Search) && filters.Search.Length > 100)
                return Error.Validation(
                    "Employee.SearchTooLong",
                    "عبارة البحث طويلة جدًا."
                    , nameof(filters.Search));
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
                    "يجب ألا يكون اسم الموظف فارغًا."
                    , nameof(request.Name));
            if(!Enum.IsDefined(typeof(EmployeeType), request.Type))
                return Error.Validation(
                    "Employee.InvalidType",
                    "يجب إدخال نوع الموظف أو النوع المحدد غير صالح."
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
            if(request.Salary.HasValue && request.Salary.Value < 0)
                return Error.Validation(
                    "Employee.NegativeSalary",
                    "يجب ألا يكون راتب الموظف سالبًا."
                    , nameof(request.Salary));
            if(!string.IsNullOrWhiteSpace(request.JobTitle) && request.JobTitle.Trim().Length > 200)
                return Error.Validation(
                    "Employee.JobTitleTooLong",
                    "يجب ألا يزيد المسمى الوظيفي للموظف عن 200 حرف."
                    , nameof(request.JobTitle));
            if (request.RequiredWorkingDaysPerMonth != null && (request.RequiredWorkingDaysPerMonth < 1 || request.RequiredWorkingDaysPerMonth > 31))
                return Error.Validation(
                    "Employee.RequiredWorkingDaysPerMonthTooLong",
                    "يجب أن يكون عدد أيام العمل المطلوبة لكل شهر بين 1 و 31."
                    , nameof(request.RequiredWorkingDaysPerMonth));
            return null;
        }

        private async Task<Error?> ValidateUpdateAsync(int id, EmployeeUpdateRequest request, CancellationToken cancellationToken)
        {
            if (request == null)
                return Error.Validation(
                    "Employee.InvalidRequest",
                    "طلب إنشاء موظف غير صالح."
                    , nameof(request));
            if(id <= 0)
                return Error.Validation(
                    "Employee.InvalidId",
                    "معرف الموظف غير صالح."
                    , nameof(id));
            if (request.Name != null && string.IsNullOrWhiteSpace(request.Name))
                return Error.Validation(
                    "Employee.InvalidName",
                    "يجب ألا يكون اسم الموظف فارغًا."
                    , nameof(request.Name));
            if (request.Type.HasValue && !Enum.IsDefined(typeof(EmployeeType), request.Type.Value))
                return Error.Validation(
                    "Employee.InvalidType",
                    "يجب إدخال نوع الموظف أو النوع المحدد غير صالح."
                    , nameof(request.Type));
            if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                var phoneExists = await dbContext.Employees
                    .AnyAsync(e => e.Id != id && e.CompanyId == campanyId && e.PhoneNumber == request.PhoneNumber.Trim(), cancellationToken);
                if (phoneExists)
                    return Error.Conflict(
                        "Employee.PhoneAlreadyExists",
                        "رقم الهاتف مستخدم بالفعل لموظف آخر.",
                        nameof(request.PhoneNumber));
            }

            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                var emailExists = await dbContext.Employees
                    .AnyAsync(e => e.Id != id && e.CompanyId == campanyId && e.Email == request.Email.Trim().ToLower(), cancellationToken);
                if (emailExists)
                    return Error.Conflict(
                        "Employee.EmailAlreadyExists",
                        "البريد الإلكتروني مستخدم بالفعل لموظف آخر.",
                        nameof(request.Email));
            }
            if (request.Salary.HasValue && request.Salary.Value < 0)
                return Error.Validation(
                    "Employee.NegativeSalary",
                    "يجب ألا يكون راتب الموظف سالبًا."
                    , nameof(request.Salary));
            if (!string.IsNullOrWhiteSpace(request.JobTitle) && request.JobTitle.Trim().Length > 200)
                return Error.Validation(
                    "Employee.JobTitleTooLong",
                    "يجب ألا يزيد المسمى الوظيفي للموظف عن 200 حرف."
                    , nameof(request.JobTitle));
            if (request.RequiredWorkingDaysPerMonth != null && (request.RequiredWorkingDaysPerMonth < 1 || request.RequiredWorkingDaysPerMonth > 31))
                return Error.Validation(
                    "Employee.RequiredWorkingDaysPerMonthTooLong",
                    "يجب أن يكون عدد أيام العمل المطلوبة لكل شهر بين 1 و 31."
                    , nameof(request.RequiredWorkingDaysPerMonth));

            return null;
        }

        private static Error InvalidFilter(string target, string description) =>
            Error.Validation(
                "Employees.InvalidFilter",
                description,
                target);
    }
}
