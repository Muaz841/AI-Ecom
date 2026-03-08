using System.Threading.Tasks;
using EcomAI.Platform.Business.Commands;
using EcomAI.Platform.Business.Security;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace EcomAI.Platform.Api.Controllers;

[ApiController]
[Authorize(Policy = PermissionCodes.ProductsManage)]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProductsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("upload")]
    [SwaggerOperation(Summary = "Upload product", Description = "Uploads a product with variants and images.")]
    [ProducesResponseType(typeof(ProductUploadResult), 200)]
    [ProducesResponseType(typeof(string), 400)]
    public async Task<IActionResult> UploadProduct([FromBody] ProductUploadCommand command)
    {
        var result = await _mediator.Send(command);
        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result.ErrorMessage);
    }
}
