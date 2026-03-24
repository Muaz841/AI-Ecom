using System;
using MediatR;

namespace EcomAI.Platform.Business.Commands;

public record DeleteProductCommand(Guid TenantId, Guid ProductId) : IRequest<ProductMutationResult>;
