using System;
using MediatR;

namespace EcomAI.Platform.Business.Commands;

public record AddProductImageCommand(
    Guid TenantId,
    Guid ProductId,
    string Url,
    string? AltText = null,
    bool IsPrimary = false) : IRequest<ProductMutationResult>;

public record DeleteProductImageCommand(
    Guid TenantId,
    Guid ProductId,
    Guid ImageId) : IRequest<ProductMutationResult>;

public record SetPrimaryImageCommand(
    Guid TenantId,
    Guid ProductId,
    Guid ImageId) : IRequest<ProductMutationResult>;
