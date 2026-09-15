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
                    "Ø¹Ø¨Ø§Ø±Ø© Ø§Ù„Ø¨Ø­Ø« Ø·ÙˆÙŠÙ„Ø© Ø¬Ø¯Ù‹Ø§."
                    , nameof(filters.Search));
            if (filters.Code?.Trim().Length > 50)
                return Error.Validation(
                    "Employee.CodeTooLong",
                    "ÙŠØ¬Ø¨ Ø£Ù„Ø§ ÙŠØ²ÙŠØ¯ ÙƒÙˆØ¯ Ø§Ù„Ù…ÙˆØ¸Ù Ø¹Ù† 50 Ø­Ø±ÙÙ‹Ø§."
                    , nameof(filters.Code));
            if (filters.Name?.Trim().Length > 200)
                return Error.Validation(
                    "Employee.NameTooLong",
                    "ÙŠØ¬Ø¨ Ø£Ù„Ø§ ÙŠØ²ÙŠØ¯ Ø§Ø³Ù… Ø§Ù„Ù…ÙˆØ¸Ù Ø¹Ù† 200 Ø­Ø±Ù."
                    , nameof(filters.Name));
            if (filters.JobTitle?.Trim().Length > 200)
                return Error.Validation(
                    "Employee.JobTitleTooLong",
                    "ÙŠØ¬Ø¨ Ø£Ù„Ø§ ÙŠØ²ÙŠØ¯ Ø§Ù„Ù…Ø³Ù…Ù‰ Ø§Ù„ÙˆØ¸ÙŠÙÙŠ Ù„Ù„Ù…ÙˆØ¸Ù Ø¹Ù† 200 Ø­Ø±Ù."
                    , nameof(filters.JobTitle));
            if (filters.    MinSalary < 0)
                return Error.Validation(
                    "Employee.MinSalaryNegative",
                    "ÙŠØ¬Ø¨ Ø£Ù„Ø§ ÙŠÙƒÙˆÙ† Ø§Ù„Ø­Ø¯ Ø§Ù„Ø£Ø¯Ù†Ù‰ Ù„Ù„Ø±Ø§ØªØ¨ Ù„Ù„Ù…ÙˆØ¸Ù Ø³Ø§Ù„Ø¨Ù‹Ø§."
                    , nameof(filters.MinSalary));
            if (filters.MaxSalary < 0)
                return Error.Validation(
                    "Employee.MaxSalaryNegative",
                    "ÙŠØ¬Ø¨ Ø£Ù„Ø§ ÙŠÙƒÙˆÙ† Ø§Ù„Ø­Ø¯ Ø§Ù„Ø£Ø¹Ù„Ù‰ Ù„Ù„Ø±Ø§ØªØ¨ Ù„Ù„Ù…ÙˆØ¸Ù Ø³Ø§Ù„Ø¨Ù‹Ø§."
                    , nameof(filters.MaxSalary));
            if (filters.EmployeeType is not null && !Enum.IsDefined(typeof(EmployeeType), filters.EmployeeType.Value))
                return Error.Validation(
                    "Employee.InvalidType",
                    "Ù†ÙˆØ¹ Ø§Ù„Ù…ÙˆØ¸Ù Ø§Ù„Ù…Ø­Ø¯Ø¯ ØºÙŠØ± ØµØ§Ù„Ø­."
                    , nameof(filters.EmployeeType));
            if (filters.PlaceName?.Trim().Length > 200)
                return Error.Validation(
                    "Employee.PlaceNameTooLong",
                    "ÙŠØ¬Ø¨ Ø£Ù„Ø§ ÙŠØ²ÙŠØ¯ Ø§Ø³Ù… Ù…ÙƒØ§Ù† Ø§Ù„Ø¹Ù…Ù„ Ø¹Ù† 200 Ø­Ø±Ù.",
                    nameof(filters.PlaceName));
            if (filters.WorkPlaceStatus.HasValue && !Enum.IsDefined(typeof(WorkPlaceStatus), filters.WorkPlaceStatus.Value))
                return Error.Validation(
                    "Employee.InvalidWorkPlaceStatus",
                    "Ø­Ø§Ù„Ø© Ù…ÙƒØ§Ù† Ø§Ù„Ø¹Ù…Ù„ ØºÙŠØ± ØµØ§Ù„Ø­Ø©.",
                    nameof(filters.WorkPlaceStatus));
            return null;
        }

        private async Task<Error?> ValidateAddAsync(EmployeeCreateRequest request, CancellationToken cancellationToken)
        {
            if(request==null)
                return Error.Validation(
                    "Employee.InvalidRequest",
                    "Ø·Ù„Ø¨ Ø¥Ù†Ø´Ø§Ø¡ Ù…ÙˆØ¸Ù ØºÙŠØ± ØµØ§Ù„Ø­."
                    , nameof(request));
            if(string.IsNullOrWhiteSpace(request.Name))
                return Error.Validation(
                    "Employee.InvalidName",
                    "ÙŠØ¬Ø¨ Ø£Ù„Ø§ ÙŠÙƒÙˆÙ† Ø§Ø³Ù… Ø§Ù„Ù…ÙˆØ¸Ù ÙØ§Ø±ØºÙ‹Ø§."
                    , nameof(request.Name));
            if (!Enum.IsDefined(typeof(EmployeeType), request.Type))
            if(!Enum.IsDefined(typeof(EmployeeType), request.Type))
                return Error.Validation(
                    "Employee.InvalidType",
                    "ÙŠØ¬Ø¨ Ø¥Ø¯Ø®Ø§Ù„ Ù†ÙˆØ¹ Ø§Ù„Ù…ÙˆØ¸Ù Ø£Ùˆ Ø§Ù„Ù†ÙˆØ¹ Ø§Ù„Ù…Ø­Ø¯Ø¯ ØºÙŠØ± ØµØ§Ù„Ø­."
                    , nameof(request.Type));
            if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                var phoneExists = await dbContext.Employees
                    .AnyAsync(e => e.CompanyId == campanyId && e.PhoneNumber == request.PhoneNumber.Trim(), cancellationToken);
                if (phoneExists)
                    return Error.Conflict(
                        "Employee.PhoneAlreadyExists",
                        "Ø±Ù‚Ù… Ø§Ù„Ù‡Ø§ØªÙ Ù…Ø³ØªØ®Ø¯Ù… Ø¨Ø§Ù„ÙØ¹Ù„ Ù„Ù…ÙˆØ¸Ù Ø¢Ø®Ø±.",
                        nameof(request.PhoneNumber));
            }

            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                var emailExists = await dbContext.Employees
                    .AnyAsync(e => e.CompanyId == campanyId && e.Email == request.Email.Trim().ToLower(), cancellationToken);
                if (emailExists)
                    return Error.Conflict(
                        "Employee.EmailAlreadyExists",
                        "Ø§Ù„Ø¨Ø±ÙŠØ¯ Ø§Ù„Ø¥Ù„ÙƒØªØ±ÙˆÙ†ÙŠ Ù…Ø³ØªØ®Ø¯Ù… Ø¨Ø§Ù„ÙØ¹Ù„ Ù„Ù…ÙˆØ¸Ù Ø¢Ø®Ø±.",
                        nameof(request.Email));
            }
            if(request.Salary.HasValue && request.Salary.Value < 0)
                return Error.Validation(
                    "Employee.NegativeSalary",
                    "ÙŠØ¬Ø¨ Ø£Ù„Ø§ ÙŠÙƒÙˆÙ† Ø±Ø§ØªØ¨ Ø§Ù„Ù…ÙˆØ¸Ù Ø³Ø§Ù„Ø¨Ù‹Ø§."
                    , nameof(request.Salary));
            if(!string.IsNullOrWhiteSpace(request.JobTitle) && request.JobTitle.Trim().Length > 200)
                return Error.Validation(
                    "Employee.JobTitleTooLong",
                    "ÙŠØ¬Ø¨ Ø£Ù„Ø§ ÙŠØ²ÙŠØ¯ Ø§Ù„Ù…Ø³Ù…Ù‰ Ø§Ù„ÙˆØ¸ÙŠÙÙŠ Ù„Ù„Ù…ÙˆØ¸Ù Ø¹Ù† 200 Ø­Ø±Ù."
                    , nameof(request.JobTitle));
            if (request.Type == EmployeeType.Daily && request.RequiredWorkingDaysPerMonth.HasValue)
                return Error.Validation(
                    "Employee.RequiredWorkingDaysNotAllowedForDaily",
                    "لا يمكن تحديد عدد أيام العمل الشهرية لموظف اليومية.",
                    nameof(request.RequiredWorkingDaysPerMonth));
            if (request.RequiredWorkingDaysPerMonth != null && (request.RequiredWorkingDaysPerMonth < 1 || request.RequiredWorkingDaysPerMonth > 31))
                return Error.Validation(
                    "Employee.RequiredWorkingDaysPerMonthTooLong",
                    "ÙŠØ¬Ø¨ Ø£Ù† ÙŠÙƒÙˆÙ† Ø¹Ø¯Ø¯ Ø£ÙŠØ§Ù… Ø§Ù„Ø¹Ù…Ù„ Ø§Ù„Ù…Ø·Ù„ÙˆØ¨Ø© Ù„ÙƒÙ„ Ø´Ù‡Ø± Ø¨ÙŠÙ† 1 Ùˆ 31."
                    , nameof(request.RequiredWorkingDaysPerMonth));
            if (!string.IsNullOrWhiteSpace(request.PlaceName) && request.PlaceName.Trim().Length > 200)
                return Error.Validation(
                    "Employee.PlaceNameTooLong",
                    "ÙŠØ¬Ø¨ Ø£Ù„Ø§ ÙŠØ²ÙŠØ¯ Ø§Ø³Ù… Ù…ÙƒØ§Ù† Ø§Ù„Ø¹Ù…Ù„ Ø¹Ù† 200 Ø­Ø±Ù.",
                    nameof(request.PlaceName));
            if (!Enum.IsDefined(typeof(WorkPlaceStatus), request.WorkPlaceStatus))
                return Error.Validation(
                    "Employee.InvalidWorkPlaceStatus",
                    "حالة مكان العمل غير صالحة. القيم المقبولة: InCompany أو OutCompany.",
                    nameof(request.WorkPlaceStatus));
            return null;
        }

        private async Task<Error?> ValidateUpdateAsync(int id, EmployeeUpdateRequest request, CancellationToken cancellationToken)
        {
            if (request == null)
                return Error.Validation(
                    "Employee.InvalidRequest",
                    "Ø·Ù„Ø¨ Ø¥Ù†Ø´Ø§Ø¡ Ù…ÙˆØ¸Ù ØºÙŠØ± ØµØ§Ù„Ø­."
                    , nameof(request));
            if(id <= 0)
                return Error.Validation(
                    "Employee.InvalidId",
                    "Ù…Ø¹Ø±Ù Ø§Ù„Ù…ÙˆØ¸Ù ØºÙŠØ± ØµØ§Ù„Ø­."
                    , nameof(id));
            if (request.Name != null && string.IsNullOrWhiteSpace(request.Name))
                return Error.Validation(
                    "Employee.InvalidName",
                    "ÙŠØ¬Ø¨ Ø£Ù„Ø§ ÙŠÙƒÙˆÙ† Ø§Ø³Ù… Ø§Ù„Ù…ÙˆØ¸Ù ÙØ§Ø±ØºÙ‹Ø§."
                    , nameof(request.Name));
            if (request.Type.HasValue && !Enum.IsDefined(typeof(EmployeeType), request.Type.Value))
                return Error.Validation(
                    "Employee.InvalidType",
                    "ÙŠØ¬Ø¨ Ø¥Ø¯Ø®Ø§Ù„ Ù†ÙˆØ¹ Ø§Ù„Ù…ÙˆØ¸Ù Ø£Ùˆ Ø§Ù„Ù†ÙˆØ¹ Ø§Ù„Ù…Ø­Ø¯Ø¯ ØºÙŠØ± ØµØ§Ù„Ø­."
                    , nameof(request.Type));
            if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                var phoneExists = await dbContext.Employees
                    .AnyAsync(e => e.Id != id && e.CompanyId == campanyId && e.PhoneNumber == request.PhoneNumber.Trim(), cancellationToken);
                if (phoneExists)
                    return Error.Conflict(
                        "Employee.PhoneAlreadyExists",
                        "Ø±Ù‚Ù… Ø§Ù„Ù‡Ø§ØªÙ Ù…Ø³ØªØ®Ø¯Ù… Ø¨Ø§Ù„ÙØ¹Ù„ Ù„Ù…ÙˆØ¸Ù Ø¢Ø®Ø±.",
                        nameof(request.PhoneNumber));
            }

            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                var emailExists = await dbContext.Employees
                    .AnyAsync(e => e.Id != id && e.CompanyId == campanyId && e.Email == request.Email.Trim().ToLower(), cancellationToken);
                if (emailExists)
                    return Error.Conflict(
                        "Employee.EmailAlreadyExists",
                        "Ø§Ù„Ø¨Ø±ÙŠØ¯ Ø§Ù„Ø¥Ù„ÙƒØªØ±ÙˆÙ†ÙŠ Ù…Ø³ØªØ®Ø¯Ù… Ø¨Ø§Ù„ÙØ¹Ù„ Ù„Ù…ÙˆØ¸Ù Ø¢Ø®Ø±.",
                        nameof(request.Email));
            }
            if (request.Salary.HasValue && request.Salary.Value < 0)
                return Error.Validation(
                    "Employee.NegativeSalary",
                    "ÙŠØ¬Ø¨ Ø£Ù„Ø§ ÙŠÙƒÙˆÙ† Ø±Ø§ØªØ¨ Ø§Ù„Ù…ÙˆØ¸Ù Ø³Ø§Ù„Ø¨Ù‹Ø§."
                    , nameof(request.Salary));
            if (!string.IsNullOrWhiteSpace(request.JobTitle) && request.JobTitle.Trim().Length > 200)
                return Error.Validation(
                    "Employee.JobTitleTooLong",
                    "ÙŠØ¬Ø¨ Ø£Ù„Ø§ ÙŠØ²ÙŠØ¯ Ø§Ù„Ù…Ø³Ù…Ù‰ Ø§Ù„ÙˆØ¸ÙŠÙÙŠ Ù„Ù„Ù…ÙˆØ¸Ù Ø¹Ù† 200 Ø­Ø±Ù."
                    , nameof(request.JobTitle));
            if (request.Type == EmployeeType.Daily && request.RequiredWorkingDaysPerMonth.HasValue)
                return Error.Validation(
                    "Employee.RequiredWorkingDaysNotAllowedForDaily",
                    "لا يمكن تحديد عدد أيام العمل الشهرية لموظف اليومية.",
                    nameof(request.RequiredWorkingDaysPerMonth));
            if (request.RequiredWorkingDaysPerMonth != null && (request.RequiredWorkingDaysPerMonth < 1 || request.RequiredWorkingDaysPerMonth > 31))
                return Error.Validation(
                    "Employee.RequiredWorkingDaysPerMonthTooLong",
                    "ÙŠØ¬Ø¨ Ø£Ù† ÙŠÙƒÙˆÙ† Ø¹Ø¯Ø¯ Ø£ÙŠØ§Ù… Ø§Ù„Ø¹Ù…Ù„ Ø§Ù„Ù…Ø·Ù„ÙˆØ¨Ø© Ù„ÙƒÙ„ Ø´Ù‡Ø± Ø¨ÙŠÙ† 1 Ùˆ 31."
                    , nameof(request.RequiredWorkingDaysPerMonth));
            if (!string.IsNullOrWhiteSpace(request.PlaceName) && request.PlaceName.Trim().Length > 200)
                return Error.Validation(
                    "Employee.PlaceNameTooLong",
                    "ÙŠØ¬Ø¨ Ø£Ù„Ø§ ÙŠØ²ÙŠØ¯ Ø§Ø³Ù… Ù…ÙƒØ§Ù† Ø§Ù„Ø¹Ù…Ù„ Ø¹Ù† 200 Ø­Ø±Ù.",
                    nameof(request.PlaceName));
            if (request.WorkPlaceStatus.HasValue && !Enum.IsDefined(typeof(WorkPlaceStatus), request.WorkPlaceStatus.Value))
                return Error.Validation(
                    "Employee.InvalidWorkPlaceStatus",
                    "حالة مكان العمل غير صالحة. القيم المقبولة: InCompany أو OutCompany.",
                    nameof(request.WorkPlaceStatus));

            return null;
        }

        private static Error InvalidFilter(string target, string description) =>
            Error.Validation(
                "Employees.InvalidFilter",
                description,
                target);
    }
}
