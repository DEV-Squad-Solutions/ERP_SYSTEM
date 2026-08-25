using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MiniErp.Api.Extensions;
using MiniErp.Application.Common.Models;
using MiniErp.Application.Features.EmployeeTransactions;

namespace MiniErp.Api.Controllers;

[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class EmployeeTransactionsController(
    IEmployeeTransactionService employeeTransactionService)
    : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<EmployeeTransactionResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] PaginationRequest pagination,
        [FromQuery] EmployeeTransactionFilterRequest filters,
        CancellationToken cancellationToken)
    {
        var result = await employeeTransactionService.GetAllAsync(
            pagination,
            filters,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<EmployeeTransactionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var result = await employeeTransactionService.GetByIdAsync(
            id,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("employee/{employeeId:int}/balance")]
    [ProducesResponseType<EmployeeAccountBalanceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBalance(
        int employeeId,
        CancellationToken cancellationToken)
    {
        var result = await employeeTransactionService.GetBalanceAsync(
            employeeId,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ProducesResponseType<EmployeeTransactionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(
        EmployeeAccountEntryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await employeeTransactionService.AddAsync(
            request,
            cancellationToken);

        return result.IsFailure
            ? this.ToProblem(result.Errors)
            : CreatedAtAction(
                nameof(GetById),
                new { id = result.Value.Id },
                result.Value);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("bulk")]
    [ProducesResponseType<List<EmployeeTransactionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateBulk(
        BulkEmployeeAccountEntryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await employeeTransactionService.AddBulkAsync(
            request,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("withdraw")]
    [ProducesResponseType<EmployeeTransactionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Withdraw(
        EmployeeWithdrawalRequest request,
        CancellationToken cancellationToken)
    {
        var result = await employeeTransactionService.WithdrawAsync(
            request,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("bulk/withdraw")]
    [ProducesResponseType<List<EmployeeTransactionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> WithdrawBulk(
        BulkEmployeeWithdrawalRequest request,
        CancellationToken cancellationToken)
    {
        var result = await employeeTransactionService.WithdrawBulkAsync(
            request,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    [ProducesResponseType<EmployeeTransactionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        int id,
        EmployeeTransactionUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await employeeTransactionService.UpdateAsync(
            id,
            request,
            cancellationToken);
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
        var result = await employeeTransactionService.DeleteAsync(
            id,
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("employee/{employeeId:int}/statement")]
    [ProducesResponseType<EmployeeStatementResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatement(
        int employeeId,
        [FromQuery] DateOnly fromDate,
        [FromQuery] DateOnly toDate,
        CancellationToken cancellationToken)
    {
        var result = await employeeTransactionService.GetStatementAsync(
            employeeId,
            fromDate,
            toDate,
            cancellationToken);
        return this.ToActionResult(result);
    }
}
