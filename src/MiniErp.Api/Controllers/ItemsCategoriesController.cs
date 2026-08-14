using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Extensions;
using MiniErp.Api.Features.ItemsCategories.Jobs;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.ItemsCategories;

namespace MiniErp.Api.Controllers;

[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class ItemsCategoriesController(
    IItemsCategoryService itemsCategoryService)
    : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<ItemsCategoryResponse>>(
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] ItemsCategoryFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var result = await itemsCategoryService.GetAllAsync(
            pagination,
            filters,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("select")]
    [ProducesResponseType<IReadOnlyList<ItemsCategorySelectResponse>>(
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSelect(
        CancellationToken cancellationToken)
    {
        var result = await itemsCategoryService.GetSelectAsync(
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<ItemsCategoryResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await itemsCategoryService.GetByIdAsync(
            id,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ProducesResponseType<ItemsCategoryResponse>(
        StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        ItemsCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await itemsCategoryService.AddAsync(
            request,
            cancellationToken);
        if (result.IsSuccess)
        {
            TryEnqueueRealtime<ItemsCategoriesRealtimeJob>(
                "Added",
                result.Value.Id,
                realtime => job => job.ExecuteAsync(realtime));
        }
        return result.IsFailure
            ? this.ToProblem(result.Error)
            : CreatedAtAction(
                nameof(GetById),
                new { id = result.Value.Id },
                result.Value);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    [ProducesResponseType<ItemsCategoryResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        int id,
        ItemsCategoryUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await itemsCategoryService.UpdateAsync(
            id,
            request,
            cancellationToken);
        if (result.IsSuccess)
        {
            TryEnqueueRealtime<ItemsCategoriesRealtimeJob>(
                "Updated",
                id,
                realtime => job => job.ExecuteAsync(realtime));
        }
        return this.ToActionResult(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await itemsCategoryService.DeleteAsync(
            id,
            cancellationToken);
        if (result.IsSuccess)
        {
            TryEnqueueRealtime<ItemsCategoriesRealtimeJob>(
                "Deleted",
                id,
                realtime => job => job.ExecuteAsync(realtime));
        }
        return this.ToActionResult(result);
    }
}
