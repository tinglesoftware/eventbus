namespace Tingle.EventBus.Diagnostics;

/// <summary>
/// Names of known headers added to events
/// </summary>
public static class HeaderNames
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public const string EventType = "X-Event-Type";
    public const string ActivityId = "X-Activity-Id";
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
