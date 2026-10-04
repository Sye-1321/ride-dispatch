using Microsoft.AspNetCore.Mvc;
using RideDispatch.Application.DriverFinancialStanding;

namespace RideDispatch.Api.Controllers;

[ApiController]
[Route("api/v1/drivers")]
public sealed class DriverFinancialStandingController : ControllerBase
{
    [HttpPut("{driverId:guid}/financial-standing")]
    public async Task<ActionResult<DriverFinancialStandingResponse>> Update(
        Guid driverId,
        UpdateDriverFinancialStandingRequest request,
        [FromServices] UpdateDriverFinancialStanding useCase,
        CancellationToken cancellationToken)
    {
        UpdateDriverFinancialStandingResult result;

        try
        {
            result = await useCase.ExecuteAsync(
                driverId,
                request.CommissionBalance,
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(nameof(request.CommissionBalance), exception.Message);
            return ValidationProblem(ModelState);
        }

        return result.Outcome switch
        {
            UpdateDriverFinancialStandingOutcome.Success =>
                Ok(DriverFinancialStandingResponse.FromView(result.FinancialStanding!)),
            UpdateDriverFinancialStandingOutcome.DriverNotFound => NotFound(),
            _ => throw new InvalidOperationException("Unknown financial-standing update outcome."),
        };
    }

    [HttpGet("{driverId:guid}/financial-standing")]
    public async Task<ActionResult<DriverFinancialStandingResponse>> Get(
        Guid driverId,
        [FromServices] GetDriverFinancialStanding useCase,
        CancellationToken cancellationToken)
    {
        var standing = await useCase.ExecuteAsync(driverId, cancellationToken);
        return standing is null
            ? NotFound()
            : Ok(DriverFinancialStandingResponse.FromView(standing));
    }
}

public sealed record UpdateDriverFinancialStandingRequest(decimal CommissionBalance);

public sealed record DriverFinancialStandingResponse(
    Guid DriverId,
    decimal CommissionBalance,
    DateTimeOffset UpdatedAt)
{
    public static DriverFinancialStandingResponse FromView(DriverFinancialStandingView standing) =>
        new(standing.DriverId, standing.CommissionBalance, standing.UpdatedAt);
}
