namespace EcomAI.Platform.Business.Constants;

/// <summary>
/// SignalR event name constants — shared between backend publishers and frontend subscribers.
/// </summary>
public static class RealtimeEventNames
{
    public const string MessageReceived = "message.received";
    public const string CommentReceived = "comment.received";
}
