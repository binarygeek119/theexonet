using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rava.Core.Dtos;
using Rava.Core.Interfaces;
using Rava.Infrastructure.Services;

namespace Rava.Api.Controllers;

[ApiController]
[Route("api/trade")]
public class TradeController(
    ITradeItemsCatalog tradeItems,
    CompanyNameService companyNameService) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("items")]
    public ActionResult<TradeItemsResponse> GetItems()
    {
        var items = tradeItems.GetAllItems()
            .Select(item => new TradeItemDto(
                item.ItemType,
                item.Category.ToString(),
                item.BasePrice,
                item.DisplayName,
                item.Color,
                item.UiSymbol,
                item.IsEmergencySource))
            .ToList();

        return Ok(new TradeItemsResponse(items));
    }

    [AllowAnonymous]
    [HttpGet("company-names")]
    public async Task<ActionResult<CompanyNameListingsResponse>> GetCompanyNameListings(CancellationToken ct)
    {
        var listings = await companyNameService.GetActiveListingsAsync(ct);
        return Ok(listings);
    }

    [Authorize]
    [HttpPost("company-names/{listingId:guid}/purchase")]
    public async Task<ActionResult<FriendActionResponse>> PurchaseCompanyName(Guid listingId, CancellationToken ct)
    {
        var (success, message) = await companyNameService.PurchaseListingAsync(
            User.GetPlayerId(),
            listingId,
            ct);

        if (!success)
        {
            return BadRequest(new { message });
        }

        return Ok(new FriendActionResponse(true, message));
    }
}
