using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Theexonet.Api.Services;
using Theexonet.Core.Dtos;

namespace Theexonet.Api.Controllers;

[ApiController]
[Route("api/store")]
public class StoreController(StoreCatalogService storeCatalog) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("catalog")]
    public async Task<ActionResult<StoreCatalogResponse>> GetCatalog(CancellationToken ct)
    {
        Guid? playerId = User.Identity?.IsAuthenticated == true ? User.GetPlayerId() : null;
        return Ok(await storeCatalog.GetCatalogAsync(playerId, ct));
    }

    [AllowAnonymous]
    [HttpGet("catalog/{slug}")]
    public ActionResult<StoreProductDto> GetProduct(string slug)
    {
        var product = storeCatalog.GetProduct(slug);
        if (product is null)
        {
            return NotFound(new { message = "Product not found." });
        }

        return Ok(product);
    }
}
