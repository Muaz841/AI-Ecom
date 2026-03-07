using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Clients;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace EcomAI.Platform.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/clients")]
public class ClientsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ClientsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [SwaggerOperation(Summary = "List clients", Description = "Returns a paginated list of clients.")]
    [ProducesResponseType(typeof(IReadOnlyList<ClientDto>), 200)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    public async Task<ActionResult<IReadOnlyList<ClientDto>>> GetAll(
        [FromQuery] int pageIndex = 0,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var clients = await _mediator.Send(new ListClientsQuery(pageIndex, pageSize));
            return Ok(clients);
        }
        catch (ValidationException ex)
        {
            return BuildValidationProblem(ex);
        }
    }

    [HttpGet("{id:guid}")]
    [SwaggerOperation(Summary = "Get client", Description = "Returns a single client by id.")]
    [ProducesResponseType(typeof(ClientDto), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    public async Task<ActionResult<ClientDto>> GetById(Guid id)
    {
        ClientDto? client;
        try
        {
            client = await _mediator.Send(new GetClientByIdQuery(id));
        }
        catch (ValidationException ex)
        {
            return BuildValidationProblem(ex);
        }

        if (client is null)
        {
            return NotFound();
        }

        return Ok(client);
    }

    [HttpPost]
    [SwaggerOperation(Summary = "Create client", Description = "Creates a new client record.")]
    [ProducesResponseType(typeof(ClientDto), 201)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    public async Task<ActionResult<ClientDto>> Create(
        [FromBody] CreateClientRequest request)
    {
        try
        {
            var created = await _mediator.Send(new CreateClientCommand(
                request.Name,
                request.BusinessName,
                request.MetaAccessToken,
                request.MetaPageId,
                request.WhatsAppBusinessAccountId,
                request.ShopifyStoreId,
                request.WooCommerceStoreId));

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ValidationException ex)
        {
            return BuildValidationProblem(ex);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id:guid}")]
    [SwaggerOperation(Summary = "Update client", Description = "Updates an existing client by id.")]
    [ProducesResponseType(typeof(ClientDto), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    public async Task<ActionResult<ClientDto>> Update(
        Guid id,
        [FromBody] UpdateClientRequest request)
    {
        ClientDto? updated;
        try
        {
            updated = await _mediator.Send(new UpdateClientCommand(
                id,
                request.Name,
                request.BusinessName,
                request.MetaAccessToken,
                request.MetaPageId,
                request.WhatsAppBusinessAccountId,
                request.ShopifyStoreId,
                request.WooCommerceStoreId));
        }
        catch (ValidationException ex)
        {
            return BuildValidationProblem(ex);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }

        if (updated is null)
        {
            return NotFound();
        }

        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [SwaggerOperation(Summary = "Delete client", Description = "Deletes an existing client by id.")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    public async Task<IActionResult> Delete(Guid id)
    {
        bool deleted;
        try
        {
            deleted = await _mediator.Send(new DeleteClientCommand(id));
        }
        catch (ValidationException ex)
        {
            return BuildValidationProblem(ex);
        }

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    private ActionResult BuildValidationProblem(ValidationException ex)
    {
        foreach (var error in ex.Errors)
        {
            ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }

        return ValidationProblem(ModelState);
    }
}

public record CreateClientRequest(
    string Name,
    string BusinessName,
    string MetaAccessToken,
    string MetaPageId,
    string WhatsAppBusinessAccountId,
    string? ShopifyStoreId,
    string? WooCommerceStoreId);

public record UpdateClientRequest(
    string Name,
    string BusinessName,
    string MetaAccessToken,
    string MetaPageId,
    string WhatsAppBusinessAccountId,
    string? ShopifyStoreId,
    string? WooCommerceStoreId);
