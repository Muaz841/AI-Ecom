using System;
using MediatR;

namespace EcomAI.Platform.Business.Commands;

public record GetProductByIdQuery(Guid TenantId, Guid ProductId) : IRequest<ProductDetailDto?>;
