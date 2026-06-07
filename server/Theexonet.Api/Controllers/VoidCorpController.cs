using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Theexonet.Api.Services.VoidCorp;
using Theexonet.Core.Dtos;

namespace Theexonet.Api.Controllers;

[ApiController]
[Route("api/public/voidcorp")]
public class VoidCorpController(VoidCorpCatalogService catalogService) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public ActionResult<VoidCorpCatalogDto> GetCatalog() =>
        Ok(catalogService.GetCatalog());

    [AllowAnonymous]
    [HttpGet("{slug}")]
    public ActionResult<VoidCorpProductDto> GetProduct(string slug)
    {
        var product = catalogService.TryGetProduct(slug);
        return product is null ? NotFound() : Ok(product);
    }
}
