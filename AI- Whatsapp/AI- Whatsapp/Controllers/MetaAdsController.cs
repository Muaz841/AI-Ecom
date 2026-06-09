using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Interfaces;
using EcomAI.Platform.Business.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace EcomAI.Platform.Api.Controllers;

[ApiController]
[Route("api/meta-ads")]
[Authorize]
public class MetaAdsController : ControllerBase
{
    private readonly IKnowledgeService      _knowledge;
    private readonly IAgentDecisionService  _decisions;

    public MetaAdsController(
        IKnowledgeService knowledgeService,
        IAgentDecisionService decisionService)
    {
        _knowledge = knowledgeService;
        _decisions = decisionService;
    }

    // ── Knowledge endpoints ───────────────────────────────────────────────────

    [HttpGet("knowledge")]
    [Authorize(Policy = PermissionCodes.MetaAdsView)]
    [SwaggerOperation(Summary = "List all active knowledge chunks for the current tenant")]
    [ProducesResponseType(typeof(List<KnowledgeChunkDto>), 200)]
    public async Task<ActionResult<List<KnowledgeChunkDto>>> GetKnowledge(CancellationToken cancellationToken)
    {
        var chunks = await _knowledge.GetChunksAsync(cancellationToken);
        return Ok(chunks);
    }

    [HttpPost("knowledge")]
    [Authorize(Policy = PermissionCodes.MetaAdsManage)]
    [SwaggerOperation(Summary = "Add a knowledge chunk to the tenant knowledge base")]
    [ProducesResponseType(typeof(KnowledgeChunkDto), 201)]
    [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
    public async Task<ActionResult<KnowledgeChunkDto>> AddKnowledge(
        [FromBody] AddKnowledgeApiRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var chunk = await _knowledge.AddChunkAsync(
                new AddKnowledgeRequest(request.Title, request.Content, request.Source),
                cancellationToken);
            return CreatedAtAction(nameof(GetKnowledge), new { }, chunk);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { title = "Validation failed", detail = ex.Message });
        }
    }

    [HttpPut("knowledge/{id:guid}")]
    [Authorize(Policy = PermissionCodes.MetaAdsManage)]
    [SwaggerOperation(Summary = "Update an existing knowledge chunk")]
    [ProducesResponseType(typeof(KnowledgeChunkDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<KnowledgeChunkDto>> UpdateKnowledge(
        Guid id,
        [FromBody] AddKnowledgeApiRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var chunk = await _knowledge.UpdateChunkAsync(
                id,
                new UpdateKnowledgeRequest(request.Title, request.Content, request.Source),
                cancellationToken);
            return Ok(chunk);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { title = "Validation failed", detail = ex.Message });
        }
    }

    [HttpDelete("knowledge/{id:guid}")]
    [Authorize(Policy = PermissionCodes.MetaAdsManage)]
    [SwaggerOperation(Summary = "Deactivate a knowledge chunk")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteKnowledge(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _knowledge.DeleteChunkAsync(id, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    // ── Agent decision endpoints ──────────────────────────────────────────────

    [HttpGet("decisions")]
    [Authorize(Policy = PermissionCodes.MetaAdsView)]
    [SwaggerOperation(Summary = "Get recent agent decisions for the current tenant")]
    [ProducesResponseType(typeof(List<AgentDecisionDto>), 200)]
    public async Task<ActionResult<List<AgentDecisionDto>>> GetDecisions(
        [FromQuery] int count = 20,
        CancellationToken cancellationToken = default)
    {
        var decisions = await _decisions.GetRecentAsync(count, cancellationToken);
        return Ok(decisions);
    }

    [HttpPost("decisions/{id:guid}/approve")]
    [Authorize(Policy = PermissionCodes.MetaAdsApprove)]
    [SwaggerOperation(Summary = "Approve a pending agent decision")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ApproveDecision(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _decisions.ApproveAsync(id, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("decisions/{id:guid}/reject")]
    [Authorize(Policy = PermissionCodes.MetaAdsApprove)]
    [SwaggerOperation(Summary = "Reject a pending agent decision")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RejectDecision(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _decisions.RejectAsync(id, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}

public sealed record AddKnowledgeApiRequest(
    [Required][MinLength(2)][MaxLength(300)] string Title,
    [Required][MinLength(10)] string Content,
    [MaxLength(500)] string? Source = null);
