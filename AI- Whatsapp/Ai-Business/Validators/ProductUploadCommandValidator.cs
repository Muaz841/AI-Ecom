using FluentValidation;

namespace EcomAI.Platform.Business.Commands;

public class ProductUploadCommandValidator : AbstractValidator<ProductUploadCommand>
{
    public ProductUploadCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("TenantId is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required.")
            .MaximumLength(300).WithMessage("Name too long (max 300 chars).");

        RuleFor(x => x.BasePrice)
            .GreaterThan(0).WithMessage("Base price must be positive.");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency required.")
            .Length(3).WithMessage("Currency must be 3-letter ISO code (e.g., PKR, USD).");

        RuleFor(x => x.Variants)
            .NotNull().WithMessage("Variants collection cannot be null.")
            .Must(v => v!.Count <= 50).WithMessage("Maximum 50 variants allowed per product.");

        RuleForEach(x => x.Variants!)
            .ChildRules(variant =>
            {
                variant.RuleFor(v => v.Size)
                    .NotEmpty().WithMessage("Size is required for each variant.");

                variant.RuleFor(v => v.Stock)
                    .GreaterThanOrEqualTo(0).WithMessage("Stock cannot be negative.");
            });

        //RuleFor(x => x.ImageUrls)
        //    .NotNull().WithMessage("ImageUrls collection cannot be null.")
        //    .Must(urls => urls!.Count <= 20)
        //    .WithMessage("Maximum 20 images per product.");

        RuleForEach(x => x.ImageUrls!)
            .NotEmpty().WithMessage("Image URL cannot be empty.")
            .MaximumLength(2000).WithMessage("Image URL too long.");
    }
}

