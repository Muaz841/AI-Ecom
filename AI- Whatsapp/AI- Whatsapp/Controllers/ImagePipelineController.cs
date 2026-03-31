using System;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;
using EcomAI.Platform.Business.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EcomAI.Platform.Api.Controllers;

public sealed record GenerateFromNewPoseResponse(
    string GeneratedImageBase64,
    string GeneratedImageMimeType,
    string PoseScript,
    string SessionToken);

public sealed record GenerateFromSavedPoseResponse(
    string GeneratedImageBase64,
    string GeneratedImageMimeType);

public sealed record SavePoseResponse(
    Guid     Id,
    string   Name,
    string   ThumbnailPath,
    DateTime CreatedAt);

[ApiController]
[Route("api/image-pipeline")]
[Authorize]
public class ImagePipelineController : ControllerBase
{
    private const int MaxFileSizeBytes = 10 * 1024 * 1024;
    private const int SessionTokenExpiryMinutes = 60;

    private readonly IAIService _aiService;
    private readonly IPosePrescriptRepository _poseRepo;
    private readonly IFileStorageService _fileStorage;
    private readonly ITokenProtector _tokenProtector;
    private readonly ITenantAIProfileService _profileService;
    private readonly IApplicationLogger _logger;

    public ImagePipelineController(
        IAIService aiService,
        IPosePrescriptRepository poseRepo,
        IFileStorageService fileStorage,
        ITokenProtector tokenProtector,
        ITenantAIProfileService profileService,
        IApplicationLogger logger)
    {
        _aiService      = aiService;
        _poseRepo       = poseRepo;
        _fileStorage    = fileStorage;
        _tokenProtector = tokenProtector;
        _profileService = profileService;
        _logger         = logger;
    }

    // Path 1: Generate from new pose reference image + dress image
    [HttpPost("generate")]
    //[Authorize(Policy = PermissionCodes.ImagesGenerate)]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> GenerateFromNewPose(
        IFormFile poseImage,
        IFormFile dressImage,
        CancellationToken cancellationToken)
    {
        if (poseImage is null || dressImage is null)
            return BadRequest(new { message = "Both poseImage and dressImage are required." });

        if (poseImage.Length > MaxFileSizeBytes || dressImage.Length > MaxFileSizeBytes)
            return BadRequest(new { message = "File size must not exceed 10 MB." });

        var allowed = new[] { "image/jpeg", "image/png", "image/webp" };
        if (!Array.Exists(allowed, t => t == poseImage.ContentType) ||
            !Array.Exists(allowed, t => t == dressImage.ContentType))
            return BadRequest(new { message = "Only JPEG, PNG, and WebP images are supported." });

        var tenantIdForGen = GetTenantId();
        if (!tenantIdForGen.HasValue) return Unauthorized();

        var profile = await _profileService.GetProfileAsync(tenantIdForGen.Value, cancellationToken);
        if (string.IsNullOrWhiteSpace(profile?.PoseExtractionPrompt))
            return BadRequest(new { message = "Pose extraction prompt is not configured. Please set it in AI Profile settings." });
        if (string.IsNullOrWhiteSpace(profile.ImageGenerationPrompt))
            return BadRequest(new { message = "Image generation prompt is not configured. Please set it in AI Profile settings." });

        var poseBytes  = await ReadBytesAsync(poseImage,  cancellationToken);
        var dressBytes = await ReadBytesAsync(dressImage, cancellationToken);

        
        var ext = await _aiService.ExtractPoseAsync(
            new PoseExtractionRequest(poseBytes, poseImage.ContentType, profile.PoseExtractionPrompt),
            cancellationToken);

        if (!ext.Success || string.IsNullOrWhiteSpace(ext.PoseScript))
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = ext.ErrorMessage ?? "Pose extraction failed." });

        // AI Call 2: generate model wearing the dress in the extracted pose
        // The admin-configured template uses {poseScript} as placeholder.
        var imagePrompt = profile.ImageGenerationPrompt.Replace("{poseScript}", ext.PoseScript, StringComparison.OrdinalIgnoreCase);
        var gen = await _aiService.GenerateModelImageAsync(
            new ImageGenerationRequest(ext.PoseScript, dressBytes, dressImage.ContentType, imagePrompt),
            cancellationToken);

        if (!gen.Success || gen.GeneratedImageBytes is null)
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = gen.ErrorMessage ?? "Image generation failed." });

        // Session token encodes expiry + pose script so the client can save later without server state
        var expiry = DateTimeOffset.UtcNow.AddMinutes(SessionTokenExpiryMinutes).ToUnixTimeSeconds();
        var sessionToken = _tokenProtector.Protect($"{expiry}||{ext.PoseScript}");

        return Ok(new GenerateFromNewPoseResponse(
            Convert.ToBase64String(gen.GeneratedImageBytes),
            gen.MimeType ?? "image/png",
            ext.PoseScript,
            sessionToken));
    }

    // Path 2: Generate from a saved pose in the library (no extraction call)
    [HttpPost("generate-from-pose")]
    [Authorize(Policy = PermissionCodes.ImagesGenerate)]
    [RequestSizeLimit(15 * 1024 * 1024)]
    public async Task<IActionResult> GenerateFromSavedPose(
        [FromForm] Guid poseId,
        IFormFile dressImage,
        CancellationToken cancellationToken)
    {
        if (dressImage is null)
            return BadRequest(new { message = "dressImage is required." });
        if (dressImage.Length > MaxFileSizeBytes)
            return BadRequest(new { message = "File size must not exceed 10 MB." });

        var tenantIdForSaved = GetTenantId();
        if (!tenantIdForSaved.HasValue) return Unauthorized();

        var profileForSaved = await _profileService.GetProfileAsync(tenantIdForSaved.Value, cancellationToken);
        if (string.IsNullOrWhiteSpace(profileForSaved?.ImageGenerationPrompt))
            return BadRequest(new { message = "Image generation prompt is not configured. Please set it in AI Profile settings." });

        var pose = await _poseRepo.GetByIdAsync(poseId, cancellationToken);
        if (pose is null) return NotFound(new { message = "Pose not found." });

        var dressBytes = await ReadBytesAsync(dressImage, cancellationToken);
        var imagePromptForSaved = profileForSaved.ImageGenerationPrompt.Replace("{poseScript}", pose.PoseScript, StringComparison.OrdinalIgnoreCase);
        var result = await _aiService.GenerateModelImageAsync(
            new ImageGenerationRequest(pose.PoseScript, dressBytes, dressImage.ContentType, imagePromptForSaved),
            cancellationToken);

        if (!result.Success || result.GeneratedImageBytes is null)
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = result.ErrorMessage ?? "Image generation failed." });

        return Ok(new GenerateFromSavedPoseResponse(
            Convert.ToBase64String(result.GeneratedImageBytes),
            result.MimeType ?? "image/png"));
    }

    // Save a confirmed pose to the library (user approved the generation result)
    [HttpPost("poses")]
    [Authorize(Policy = PermissionCodes.ImagesGenerate)]
    [RequestSizeLimit(15 * 1024 * 1024)]
    public async Task<IActionResult> SavePose(
        [FromForm] string sessionToken,
        [FromForm] string name,
        IFormFile referenceImage,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionToken) || string.IsNullOrWhiteSpace(name))
            return BadRequest(new { message = "sessionToken and name are required." });
        if (referenceImage is null)
            return BadRequest(new { message = "referenceImage is required." });

        string poseScript;
        try
        {
            var plain = _tokenProtector.Unprotect(sessionToken);
            var sep   = plain.IndexOf("||", StringComparison.Ordinal);
            if (sep < 0) return BadRequest(new { message = "Invalid session token." });

            var expirySeconds = long.Parse(plain[..sep]);
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expirySeconds)
                return BadRequest(new { message = "Session token expired. Please regenerate the pose." });

            poseScript = plain[(sep + 2)..];
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Session token unprotect failed.");
            return BadRequest(new { message = "Invalid or tampered session token." });
        }

        var tenantId = GetTenantId();
        if (!tenantId.HasValue) return Unauthorized();

        var imagePath = await _fileStorage.SavePoseImageAsync(
            tenantId.Value,
            referenceImage.OpenReadStream(),
            referenceImage.FileName,
            referenceImage.ContentType,
            cancellationToken);

        var pose = PosePrescript.Create(tenantId.Value, name.Trim(), poseScript, imagePath, GetUserId());
        await _poseRepo.AddAsync(pose, cancellationToken);
        await _poseRepo.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetPoses), null,
            new SavePoseResponse(pose.Id, pose.Name, pose.ReferenceImagePath, pose.CreatedAt));
    }

    // List all active poses in the tenant's library
    [HttpGet("poses")]
    [Authorize(Policy = PermissionCodes.ImagesRead)]
    public async Task<IActionResult> GetPoses(CancellationToken cancellationToken)
        => Ok(await _poseRepo.GetActiveByTenantAsync(cancellationToken));

    
    [HttpDelete("poses/{id:guid}")]
    [Authorize(Policy = PermissionCodes.ImagesManage)]
    public async Task<IActionResult> DeletePose(Guid id, CancellationToken cancellationToken)
    {
        var pose = await _poseRepo.GetByIdAsync(id, cancellationToken);
        if (pose is null) return NotFound(new { message = "Pose not found." });
        pose.Deactivate();
        await _poseRepo.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static async Task<byte[]> ReadBytesAsync(IFormFile file, CancellationToken ct)
    {
        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    private Guid? GetTenantId()
    {
        var claim = User.FindFirstValue("tenant_id");
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
}
