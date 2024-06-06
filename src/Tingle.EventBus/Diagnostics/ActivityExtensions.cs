using System.Diagnostics;

namespace Tingle.EventBus.Diagnostics;

///
public static class ActivityExtensions
{
    // Copied from https://github.com/dotnet/runtime/pull/102905/files
#if !NET9_0_OR_GREATER
    ///
    public static Activity AddException(this Activity activity, Exception exception, TagList tags = default, DateTimeOffset timestamp = default)
    {
        if (activity is null) throw new ArgumentNullException(nameof(activity));
        if (exception is null) throw new ArgumentNullException(nameof(exception));

        TagList exceptionTags = tags;

        const string ExceptionEventName = "exception";
        const string ExceptionMessageTag = "exception.message";
        const string ExceptionStackTraceTag = "exception.stacktrace";
        const string ExceptionTypeTag = "exception.type";

        bool hasMessage = false;
        bool hasStackTrace = false;
        bool hasType = false;

        for (int i = 0; i < exceptionTags.Count; i++)
        {
            if (exceptionTags[i].Key == ExceptionMessageTag)
            {
                hasMessage = true;
            }
            else if (exceptionTags[i].Key == ExceptionStackTraceTag)
            {
                hasStackTrace = true;
            }
            else if (exceptionTags[i].Key == ExceptionTypeTag)
            {
                hasType = true;
            }
        }

        if (!hasMessage)
        {
            exceptionTags.Add(new KeyValuePair<string, object?>(ExceptionMessageTag, exception.Message));
        }

        if (!hasStackTrace)
        {
            exceptionTags.Add(new KeyValuePair<string, object?>(ExceptionStackTraceTag, exception.ToString()));
        }

        if (!hasType)
        {
            exceptionTags.Add(new KeyValuePair<string, object?>(ExceptionTypeTag, exception.GetType().ToString()));
        }

        return activity.AddEvent(new ActivityEvent(ExceptionEventName, timestamp));
    }
#endif
}
