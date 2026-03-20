namespace EcomAI.Platform.Business.Constants;

/// <summary>
/// Canonical AI intent classification codes.
/// These match the values the LLM is prompted to return — never change them without
/// updating the prompt strings in each AI service.
/// </summary>
public static class AiIntentCodes
{
    public const string Greeting    = "greeting";
    public const string OrderStart  = "order_start";
    public const string Inquiry     = "inquiry";
    public const string Complaint   = "complaint";
    public const string Unhandled   = "unhandled";

    public static readonly IReadOnlyList<string> All =
    [
        Greeting,
        OrderStart,
        Inquiry,
        Complaint,
        Unhandled
    ];
}
