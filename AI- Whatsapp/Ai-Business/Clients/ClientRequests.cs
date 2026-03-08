using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EcomAI.Platform.Business.Entities;
using EcomAI.Platform.Business.Interfaces;
using FluentValidation;
using MediatR;

namespace EcomAI.Platform.Business.Clients;

public record CreateClientCommand(
    string Name,
    string BusinessName,
    string? MetaAccessToken,
    string? MetaPageId,
    string? WhatsAppBusinessAccountId,
    string? ShopifyStoreId,
    string? WooCommerceStoreId) : IRequest<ClientDto>;

public record UpdateClientCommand(
    Guid Id,
    string Name,
    string BusinessName,
    string? MetaAccessToken,
    string? MetaPageId,
    string? WhatsAppBusinessAccountId,
    string? ShopifyStoreId,
    string? WooCommerceStoreId) : IRequest<ClientDto?>;

public record DeleteClientCommand(Guid Id) : IRequest<bool>;

public record GetClientByIdQuery(Guid Id) : IRequest<ClientDto?>;

public record ListClientsQuery(int PageIndex = 0, int PageSize = 50) : IRequest<IReadOnlyList<ClientDto>>;

public class CreateClientCommandHandler : IRequestHandler<CreateClientCommand, ClientDto>
{
    private readonly IRepository<Tenant> _tenantRepository;
    private readonly IRepository<ClientSecrets> _clientSecretsRepository;

    public CreateClientCommandHandler(
        IRepository<Tenant> tenantRepository,
        IRepository<ClientSecrets> clientSecretsRepository)
    {
        _tenantRepository = tenantRepository;
        _clientSecretsRepository = clientSecretsRepository;
    }

    public async Task<ClientDto> Handle(CreateClientCommand request, CancellationToken cancellationToken)
    {
        var tenant = Tenant.Create(
            request.Name,
            request.BusinessName);

        await _tenantRepository.AddAsync(tenant);
        await _tenantRepository.SaveChangesAsync();

        var clientSecrets = ClientSecrets.CreateForTenant(tenant.Id);
        await _clientSecretsRepository.AddAsync(clientSecrets);
        await _clientSecretsRepository.SaveChangesAsync();

        return ClientMapper.ToDto(tenant, clientSecrets);
    }
}

public class UpdateClientCommandHandler : IRequestHandler<UpdateClientCommand, ClientDto?>
{
    private readonly IRepository<Tenant> _tenantRepository;
    private readonly IRepository<ClientSecrets> _clientSecretsRepository;

    public UpdateClientCommandHandler(
        IRepository<Tenant> tenantRepository,
        IRepository<ClientSecrets> clientSecretsRepository)
    {
        _tenantRepository = tenantRepository;
        _clientSecretsRepository = clientSecretsRepository;
    }

    public async Task<ClientDto?> Handle(UpdateClientCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByIdAsync(request.Id);
        if (tenant is null)
        {
            return null;
        }

        tenant.UpdateProfile(request.Name, request.BusinessName);
        await _tenantRepository.UpdateAsync(tenant);
        await _tenantRepository.SaveChangesAsync();

        var clientSecrets = await _clientSecretsRepository.FirstOrDefaultAsync(x => x.TenantRefId == tenant.Id);
        var isNewSecrets = clientSecrets is null;
        if (clientSecrets is null)
        {
            clientSecrets = ClientSecrets.CreateForTenant(tenant.Id);
            await _clientSecretsRepository.AddAsync(clientSecrets);
        }

        clientSecrets.UpdateMetaConfiguration(
            request.MetaAccessToken,
            request.MetaPageId,
            request.WhatsAppBusinessAccountId);
        clientSecrets.UpdateStoreConfiguration(request.ShopifyStoreId, request.WooCommerceStoreId);

        if (!isNewSecrets)
        {
            await _clientSecretsRepository.UpdateAsync(clientSecrets);
        }
        await _clientSecretsRepository.SaveChangesAsync();

        return ClientMapper.ToDto(tenant, clientSecrets);
    }
}

public class DeleteClientCommandHandler : IRequestHandler<DeleteClientCommand, bool>
{
    private readonly IRepository<Tenant> _tenantRepository;

    public DeleteClientCommandHandler(IRepository<Tenant> tenantRepository)
    {
        _tenantRepository = tenantRepository;
    }

    public async Task<bool> Handle(DeleteClientCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByIdAsync(request.Id);
        if (tenant is null)
        {
            return false;
        }

        await _tenantRepository.DeleteAsync(tenant);
        await _tenantRepository.SaveChangesAsync();
        return true;
    }
}

public class GetClientByIdQueryHandler : IRequestHandler<GetClientByIdQuery, ClientDto?>
{
    private readonly IRepository<Tenant> _tenantRepository;
    private readonly IRepository<ClientSecrets> _clientSecretsRepository;

    public GetClientByIdQueryHandler(
        IRepository<Tenant> tenantRepository,
        IRepository<ClientSecrets> clientSecretsRepository)
    {
        _tenantRepository = tenantRepository;
        _clientSecretsRepository = clientSecretsRepository;
    }

    public async Task<ClientDto?> Handle(GetClientByIdQuery request, CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByIdAsync(request.Id);
        if (tenant is null)
        {
            return null;
        }

        var clientSecrets = await _clientSecretsRepository.FirstOrDefaultAsync(x => x.TenantRefId == tenant.Id);
        return ClientMapper.ToDto(tenant, clientSecrets);
    }
}

public class ListClientsQueryHandler : IRequestHandler<ListClientsQuery, IReadOnlyList<ClientDto>>
{
    private readonly IRepository<Tenant> _tenantRepository;
    private readonly IRepository<ClientSecrets> _clientSecretsRepository;

    public ListClientsQueryHandler(
        IRepository<Tenant> tenantRepository,
        IRepository<ClientSecrets> clientSecretsRepository)
    {
        _tenantRepository = tenantRepository;
        _clientSecretsRepository = clientSecretsRepository;
    }

    public async Task<IReadOnlyList<ClientDto>> Handle(ListClientsQuery request, CancellationToken cancellationToken)
    {
        var tenants = await _tenantRepository.ListAsync(
            orderBy: q => q.OrderBy(c => c.BusinessName),
            pageIndex: request.PageIndex,
            pageSize: request.PageSize);

        if (tenants.Count == 0)
        {
            return [];
        }

        var tenantIds = tenants.Select(x => x.Id).ToList();
        var allSecrets = await _clientSecretsRepository.ListAsync(x => tenantIds.Contains(x.TenantRefId), pageSize: 0);
        var secretsByTenant = allSecrets.ToDictionary(x => x.TenantRefId, x => x);

        return tenants
            .Select(tenant =>
            {
                secretsByTenant.TryGetValue(tenant.Id, out var secrets);
                return ClientMapper.ToDto(tenant, secrets);
            })
            .ToList();
    }
}

public class CreateClientCommandValidator : AbstractValidator<CreateClientCommand>
{
    public CreateClientCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BusinessName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MetaPageId).MaximumLength(200);
        RuleFor(x => x.WhatsAppBusinessAccountId).MaximumLength(200);
    }
}

public class UpdateClientCommandValidator : AbstractValidator<UpdateClientCommand>
{
    public UpdateClientCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BusinessName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MetaPageId).MaximumLength(200);
        RuleFor(x => x.WhatsAppBusinessAccountId).MaximumLength(200);
    }
}

public class DeleteClientCommandValidator : AbstractValidator<DeleteClientCommand>
{
    public DeleteClientCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class GetClientByIdQueryValidator : AbstractValidator<GetClientByIdQuery>
{
    public GetClientByIdQueryValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class ListClientsQueryValidator : AbstractValidator<ListClientsQuery>
{
    public ListClientsQueryValidator()
    {
        RuleFor(x => x.PageIndex).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}

internal static class ClientMapper
{
    public static ClientDto ToDto(Tenant tenant, ClientSecrets? clientSecrets)
    {
        return new ClientDto(
            tenant.Id,
            tenant.Name,
            tenant.BusinessName,
            clientSecrets?.MetaPageId,
            clientSecrets?.WhatsAppBusinessAccountId,
            clientSecrets?.ShopifyStoreId,
            clientSecrets?.WooCommerceStoreId,
            tenant.CreatedAt,
            tenant.LastSyncedAt);
    }
}
