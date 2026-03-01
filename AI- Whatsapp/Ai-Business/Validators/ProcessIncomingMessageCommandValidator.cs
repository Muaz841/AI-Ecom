using FluentValidation;

namespace EcomAI.Platform.Business.Commands;

public class ProcessIncomingMessageCommandValidator : AbstractValidator<ProcessIncomingMessageCommand>
{
    public ProcessIncomingMessageCommandValidator()
    {
        RuleFor(x => x.ClientId)
            .NotEmpty().WithMessage("ClientId is required.");

        RuleFor(x => x.Platform)
            .NotEmpty().WithMessage("Platform is required.")
            .Must(BeSupportedPlatform)
            .WithMessage("Platform must be 'whatsapp' or 'instagram'.");

        RuleFor(x => x.From)
            .NotEmpty().WithMessage("From (sender) is required.")
            .MaximumLength(100);

        RuleFor(x => x.To)
            .NotEmpty().WithMessage("To (recipient) is required.")
            .MaximumLength(100);

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Message content is required.")
            .MaximumLength(4096).WithMessage("Content too long (max 4096 chars).");
    }

    private static bool BeSupportedPlatform(string platform)
    {
        if (string.IsNullOrWhiteSpace(platform))
        {
            return false;
        }

        var value = platform.Trim().ToLowerInvariant();
        return value is "whatsapp" or "instagram";
    }
}
