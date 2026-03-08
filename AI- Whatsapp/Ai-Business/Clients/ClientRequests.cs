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
    private readonly IRepository<Client> _repository;

    public CreateClientCommandHandler(IRepository<Client> repository)
    {
        _repository = repository;
    }

    public async Task<ClientDto> Handle(CreateClientCommand request, CancellationToken cancellationToken)
    {
        var client = Client.Create(
            request.Name,
            request.BusinessName,
            request.MetaAccessToken,
            request.MetaPageId,
            request.WhatsAppBusinessAccountId,
            request.ShopifyStoreId,
            request.WooCommerceStoreId);

        await _repository.AddAsync(client);
        await _repository.SaveChangesAsync();

        return ClientMapper.ToDto(client);
    }
}

public class UpdateClientCommandHandler : IRequestHandler<UpdateClientCommand, ClientDto?>
{
    private readonly IRepository<Client> _repository;

    public UpdateClientCommandHandler(IRepository<Client> repository)
    {
        _repository = repository;
    }

    public async Task<ClientDto?> Handle(UpdateClientCommand request, CancellationToken cancellationToken)
    {
        var client = await _repository.GetByIdAsync(request.Id);
        if (client is null)
        {
            return null;
        }

        client.UpdateProfile(
            request.Name,
            request.BusinessName,
            request.ShopifyStoreId,
            request.WooCommerceStoreId);

        client.UpdateMetaConfiguration(
            request.MetaAccessToken,
            request.MetaPageId,
            request.WhatsAppBusinessAccountId);

        await _repository.UpdateAsync(client);
        await _repository.SaveChangesAsync();

        return ClientMapper.ToDto(client);
    }
}

public class DeleteClientCommandHandler : IRequestHandler<DeleteClientCommand, bool>
{
    private readonly IRepository<Client> _repository;

    public DeleteClientCommandHandler(IRepository<Client> repository)
    {
        _repository = repository;
    }

    public async Task<bool> Handle(DeleteClientCommand request, CancellationToken cancellationToken)
    {
        var client = await _repository.GetByIdAsync(request.Id);
        if (client is null)
        {
            return false;
        }

        await _repository.DeleteAsync(client);
        await _repository.SaveChangesAsync();
        return true;
    }
}

public class GetClientByIdQueryHandler : IRequestHandler<GetClientByIdQuery, ClientDto?>
{
    private readonly IRepository<Client> _repository;

    public GetClientByIdQueryHandler(IRepository<Client> repository)
    {
        _repository = repository;
    }

    public async Task<ClientDto?> Handle(GetClientByIdQuery request, CancellationToken cancellationToken)
    {
        var client = await _repository.GetByIdAsync(request.Id);
        return client is null ? null : ClientMapper.ToDto(client);
    }
}

public class ListClientsQueryHandler : IRequestHandler<ListClientsQuery, IReadOnlyList<ClientDto>>
{
    private readonly IRepository<Client> _repository;

    public ListClientsQueryHandler(IRepository<Client> repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<ClientDto>> Handle(ListClientsQuery request, CancellationToken cancellationToken)
    {
        var clients = await _repository.ListAsync(
            orderBy: q => q.OrderBy(c => c.BusinessName),
            pageIndex: request.PageIndex,
            pageSize: request.PageSize);

        return clients.Select(ClientMapper.ToDto).ToList();
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
    public static ClientDto ToDto(Client client)
    {
        return new ClientDto(
            client.Id,
            client.Name,
            client.BusinessName,
            client.MetaPageId,
            client.WhatsAppBusinessAccountId,
            client.ShopifyStoreId,
            client.WooCommerceStoreId,
            client.CreatedAt,
            client.LastSyncedAt);
    }
}
