using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Extensions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.StoreContainers;

namespace MiniErp.Api.Controllers;

[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class StoreContainersController(
    IStoreContainerService storeContainerService)
    : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<StoreContainerResponse>>(
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] StoreContainerFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var result = await storeContainerService.GetAllAsync(
            pagination,
            filters,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("select")]
    [ProducesResponseType<IReadOnlyList<SelectResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetSelect(
        [FromQuery] int storeId,
        CancellationToken cancellationToken)
    {
        var result = await storeContainerService.GetSelectAsync(
            storeId,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("workspace")]
    [ProducesResponseType<StoreContainerWorkspaceResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetWorkspace(
        [FromQuery] int storeId,
        CancellationToken cancellationToken)
    {
        var result = await storeContainerService.GetWorkspaceAsync(
            storeId,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<StoreContainerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await storeContainerService.GetByIdAsync(
            id,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("upsert")]
    [ProducesResponseType<IReadOnlyList<StoreContainerResponse>>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Upsert(
        StoreContainerUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var result = await storeContainerService.UpsertAsync(
            request,
            cancellationToken);
        return this.ToActionResult(result);
    }
}
