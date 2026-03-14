using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EcomAI.Platform.Business.Interfaces;

public interface IMetaIntegrationService
{
    Task<MetaConnectStartResult> StartConnectionAsync(
        Guid tenantId,
        Guid userId,
        string channel,
        string? returnUrl = null,
        CancellationToken cancellationToken = default);

    Task<MetaConnectCallbackResult> CompleteConnectionAsync(
        string state,
        string code,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MetaConnectionDto>> ListConnectionsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<bool> DisconnectAsync(
        Guid tenantId,
        Guid connectionId,
        CancellationToken cancellationToken = default);
}

public sealed record MetaConnectStartResult(
    bool Success,
    string? AuthorizationUrl,
    string? State,
    string? ErrorMessage = null);

public sealed record MetaConnectCallbackResult(
    bool Success,
    Guid? ConnectionId,
    string? Status,
    string? ReturnUrl,
    string? ErrorMessage = null);

public sealed record MetaAssetDto(
    Guid Id,
    string AssetType,
    string ExternalId,
    string? ExternalName,
    bool IsActive);

public sealed record MetaConnectionDto(
    Guid Id,
    string Channel,
    string Status,
    string? ExternalBusinessId,
    string? ExternalAccountId,
    DateTime ConnectedAtUtc,
    DateTime? AccessTokenExpiresAtUtc,
    DateTime? LastValidatedAtUtc,
    string? LastError,
    IReadOnlyList<MetaAssetDto> Assets);
