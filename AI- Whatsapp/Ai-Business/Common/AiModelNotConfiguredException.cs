namespace EcomAI.Platform.Business.Common;

/// <summary>
/// Thrown by an AI service when the active provider has no model selected.
/// The message handler catches this and sends a user-visible prompt to select a model.
/// </summary>
public sealed class AiModelNotConfiguredException : InvalidOperationException
{
    public AiModelNotConfiguredException()
        : base("AI model not configured. Ask your host to select a model in AI Provider Settings.") { }
}
