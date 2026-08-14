using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Extensions;
using MiniErp.Api.Features.Users.Jobs;
using MiniErp.Application.Common.Authentication;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.Users;

namespace MiniErp.Api.Controllers;

[Authorize(Roles = ApplicationRoles.Admin)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class UsersController(IUserService userService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<UserResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] UserFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var result = await userService.GetAllAsync(
            pagination,
            filters,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("roles")]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRoles(CancellationToken cancellationToken)
    {
        var result = await userService.GetRolesAsync(cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await userService.GetByIdAsync(id, cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        UserCreateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await userService.AddAsync(request, cancellationToken);

        if (result.IsSuccess)
        {
            var operationId = Guid.NewGuid();
            foreach (var companyId in request.CompanyIds.Distinct())
            {
                TryEnqueueRealtime<UsersRealtimeJob>(
                    "Added",
                    result.Value.Id,
                    realtime => job => job.ExecuteAsync(realtime),
                    companyId,
                    operationId);
            }
        }

        return result.IsFailure
            ? this.ToProblem(result.Error)
            : CreatedAtAction(
                nameof(GetById),
                new { id = result.Value.Id },
                result.Value);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        UserUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var previous = await userService.GetByIdAsync(id, cancellationToken);
        var result = await userService.UpdateAsync(
            id,
            request,
            cancellationToken);
        if (result.IsSuccess)
        {
            var operationId = Guid.NewGuid();
            var companyIds = request.CompanyIds
                .Concat(previous.IsSuccess
                    ? previous.Value.Companies.Select(company => company.Id)
                    : [])
                .Distinct();
            foreach (var companyId in companyIds)
            {
                TryEnqueueRealtime<UsersRealtimeJob>(
                    "Updated",
                    id,
                    realtime => job => job.ExecuteAsync(realtime),
                    companyId,
                    operationId);
            }
        }
        return this.ToActionResult(result);
    }

    [HttpPut("{id:guid}/companies")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignCompanies(
        Guid id,
        UserCompaniesRequest request,
        CancellationToken cancellationToken)
    {
        var previous = await userService.GetByIdAsync(id, cancellationToken);
        var result = await userService.AssignCompaniesAsync(
            id,
            request,
            cancellationToken);
        if (result.IsSuccess)
        {
            var operationId = Guid.NewGuid();
            var companyIds = request.CompanyIds
                .Concat(previous.IsSuccess
                    ? previous.Value.Companies.Select(company => company.Id)
                    : [])
                .Distinct();
            foreach (var companyId in companyIds)
            {
                TryEnqueueRealtime<UsersRealtimeJob>(
                    "Updated",
                    id,
                    realtime => job => job.ExecuteAsync(realtime),
                    companyId,
                    operationId);
            }
        }
        return this.ToActionResult(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var previous = await userService.GetByIdAsync(id, cancellationToken);
        var result = await userService.DeleteAsync(id, cancellationToken);
        if (result.IsSuccess)
        {
            var operationId = Guid.NewGuid();
            var companyIds = previous.IsSuccess
                ? previous.Value.Companies.Select(company => company.Id)
                : [];
            foreach (var companyId in companyIds.Distinct())
            {
                TryEnqueueRealtime<UsersRealtimeJob>(
                    "Deleted",
                    id,
                    realtime => job => job.ExecuteAsync(realtime),
                    companyId,
                    operationId);
            }
        }
        return this.ToActionResult(result);
    }
}
