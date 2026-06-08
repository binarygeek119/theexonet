using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Theexonet.Core.Dtos;
using Theexonet.Core.Interfaces;
using Theexonet.Infrastructure.Services;

namespace Theexonet.Api.Controllers;

[ApiController]
[Route("api/trade")]
public class TradeController(
    ITradeItemsCatalog tradeItems,
    CompanyNameService companyNameService,
    TradeAuctionService tradeAuctionService,
    TradeListingService tradeListingService) : ControllerBase
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
    [HttpGet("market-info")]
    public async Task<ActionResult<TradeMarketInfoResponse>> GetMarketInfo(CancellationToken ct) =>
        Ok(await tradeAuctionService.GetMarketInfoAsync(ct));

    [AllowAnonymous]
    [HttpGet("listings")]
    public async Task<ActionResult<TradeListingListResponse>> GetListings(CancellationToken ct)
    {
        Guid? viewerId = User.Identity?.IsAuthenticated == true ? User.GetPlayerId() : null;
        return Ok(await tradeListingService.GetListingsAsync(viewerId, ct));
    }

    [Authorize]
    [HttpPost("listings")]
    public async Task<ActionResult<TradeListingActionResponse>> CreateListing(
        CreateTradeListingRequest request,
        CancellationToken ct)
    {
        var (result, error) = await tradeListingService.CreateListingAsync(User.GetPlayerId(), request, ct);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(result);
    }

    [Authorize]
    [HttpPost("listings/{listingId:guid}/purchase")]
    public async Task<ActionResult<TradeListingActionResponse>> PurchaseListing(
        Guid listingId,
        CancellationToken ct)
    {
        var (result, error) = await tradeListingService.PurchaseListingAsync(User.GetPlayerId(), listingId, ct);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(result);
    }

    [Authorize]
    [HttpDelete("listings/{listingId:guid}")]
    public async Task<ActionResult<TradeListingActionResponse>> CancelListing(Guid listingId, CancellationToken ct)
    {
        var (result, error) = await tradeListingService.CancelListingAsync(User.GetPlayerId(), listingId, ct);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(result);
    }

    [AllowAnonymous]
    [HttpGet("auctions")]
    public async Task<ActionResult<TradeAuctionListResponse>> GetAuctions(CancellationToken ct)
    {
        Guid? viewerId = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            viewerId = User.GetPlayerId();
        }

        return Ok(await tradeAuctionService.GetActiveAuctionsAsync(viewerId, ct));
    }

    [Authorize]
    [HttpPost("auctions")]
    public async Task<ActionResult<TradeAuctionActionResponse>> CreateAuction(
        CreateTradeAuctionRequest request,
        CancellationToken ct)
    {
        var (result, error) = await tradeAuctionService.CreateAuctionAsync(User.GetPlayerId(), request, ct);
        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(result);
    }

    [Authorize]
    [HttpPost("auctions/{auctionId:guid}/bid")]
    public async Task<ActionResult<TradeAuctionActionResponse>> PlaceBid(
        Guid auctionId,
        PlaceTradeAuctionBidRequest request,
        CancellationToken ct)
    {
        var (result, error) = await tradeAuctionService.PlaceBidAsync(
            User.GetPlayerId(),
            auctionId,
            request.BidAmount,
            ct);

        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(result);
    }

    [Authorize]
    [HttpDelete("auctions/{auctionId:guid}")]
    public async Task<ActionResult<TradeAuctionActionResponse>> CancelAuction(Guid auctionId, CancellationToken ct)
    {
        var (result, error) = await tradeAuctionService.CancelAuctionAsync(
            User.GetPlayerId(),
            auctionId,
            ct);

        if (error is not null)
        {
            return BadRequest(new { message = error });
        }

        return Ok(result);
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
