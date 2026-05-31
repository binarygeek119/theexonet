using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rava.Core.Dtos;
using Rava.Core.Interfaces;

namespace Rava.Api.Controllers;

[ApiController]
[Route("api/trade")]
public class TradeController(ITradeItemsCatalog tradeItems) : ControllerBase
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
}
