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
        string channel,
        string state,
        string code,
        string? returnUrl = null,
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
    string? ErrorMessage = null);

public sealed record MetaConnectionDto(
    Guid Id,
    string Channel,
    string Status,
    string? ExternalBusinessId,
    string? ExternalAccountId,
    DateTime ConnectedAtUtc,
    DateTime? AccessTokenExpiresAtUtc,
    DateTime? LastValidatedAtUtc,
    string? LastError);
