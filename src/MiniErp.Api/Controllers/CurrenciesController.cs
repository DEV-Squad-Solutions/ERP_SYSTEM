using Microsoft.AspNetCore.Mvc;
using MiniErp.Application.Features.Currencies;
using MiniErp.Domain.Enums;

namespace MiniErp.Api.Controllers;

[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class CurrenciesController : ApiControllerBase
{
    [HttpGet("select")]
    [ProducesResponseType<IReadOnlyList<CurrencyOptionResponse>>(
        StatusCodes.Status200OK)]
    public IActionResult GetSelect()
    {
        var response = Enum.GetValues<CurrencyCode>()
            .Select(currency => new CurrencyOptionResponse(
                Value: currency,
                Description: currency.GetDescription()))
            .ToArray();

        return Ok(response);
    }
}
