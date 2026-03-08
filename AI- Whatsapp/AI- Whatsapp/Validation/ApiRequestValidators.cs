using System;
using FluentValidation;
using EcomAI.Platform.Api.Controllers;
using EcomAI.Platform.Business.Interfaces;

namespace EcomAI.Platform.Api.Validation;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Role).NotEmpty().MaximumLength(50);
    }
}

public sealed class LoginApiRequestValidator : AbstractValidator<LoginApiRequest>
{
    public LoginApiRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x)
            .Must(x => x.TenantId != Guid.Empty || !string.IsNullOrWhiteSpace(x.TenantName))
            .WithMessage("TenantId or tenantName is required.");
    }
}

public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
    }
}

public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(4000);
    }
}

public sealed class ResetPasswordApiRequestValidator : AbstractValidator<ResetPasswordApiRequest>
{
    public ResetPasswordApiRequestValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.ResetToken).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8);
    }
}

public sealed class TenantScopedQueryValidator : AbstractValidator<TenantScopedQuery>
{
    public TenantScopedQueryValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
    }
}

public sealed class UpdatePermissionApiRequestValidator : AbstractValidator<UpdatePermissionApiRequest>
{
    public UpdatePermissionApiRequestValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public sealed class CreatePermissionRequestValidator : AbstractValidator<CreatePermissionRequest>
{
    public CreatePermissionRequestValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public sealed class UpdateRoleApiRequestValidator : AbstractValidator<UpdateRoleApiRequest>
{
    public UpdateRoleApiRequestValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public sealed class CreateRoleRequestValidator : AbstractValidator<CreateRoleRequest>
{
    public CreateRoleRequestValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public sealed class SetRolePermissionsRequestValidator : AbstractValidator<SetRolePermissionsRequest>
{
    public SetRolePermissionsRequestValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.PermissionIds).NotNull();
        RuleForEach(x => x.PermissionIds).NotEmpty();
    }
}

public sealed class CreateClientRequestValidator : AbstractValidator<CreateClientRequest>
{
    public CreateClientRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BusinessName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MetaPageId).MaximumLength(200);
        RuleFor(x => x.WhatsAppBusinessAccountId).MaximumLength(200);
        RuleFor(x => x.ShopifyStoreId).MaximumLength(200);
        RuleFor(x => x.WooCommerceStoreId).MaximumLength(200);
    }
}

public sealed class UpdateClientRequestValidator : AbstractValidator<UpdateClientRequest>
{
    public UpdateClientRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BusinessName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MetaPageId).MaximumLength(200);
        RuleFor(x => x.WhatsAppBusinessAccountId).MaximumLength(200);
        RuleFor(x => x.ShopifyStoreId).MaximumLength(200);
        RuleFor(x => x.WooCommerceStoreId).MaximumLength(200);
    }
}

public sealed class ListConversationsQueryRequestValidator : AbstractValidator<ListConversationsQueryRequest>
{
    public ListConversationsQueryRequestValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.PageIndex).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}

public sealed class ConversationMessagesQueryRequestValidator : AbstractValidator<ConversationMessagesQueryRequest>
{
    public ConversationMessagesQueryRequestValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.PageIndex).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}

public sealed class MetaStartConnectionRequestValidator : AbstractValidator<MetaStartConnectionRequest>
{
    public MetaStartConnectionRequestValidator()
    {
        RuleFor(x => x.ReturnUrl).MaximumLength(2000);
    }
}
