namespace System;

internal static class TimeSpanExtensions
{
    /// <summary>Generates a readable string.</summary>
    /// <param name="span"></param>
    /// <returns></returns>
    public static string ToReadableString(this TimeSpan span)
    {
        var parts = new List<string?>
        {
            (span.Days / 7) > 0 ? $"{(span.Days / 7):0} weeks" : null,
            span.Days % 7 > 0 ? $"{(span.Days % 7):0} days" : null,
            span.Hours > 0 ? $"{span.Hours:0} hours" : null,
            span.Minutes > 0 ? $"{span.Minutes:0} min" : null,
            span.Seconds > 0 ? $"{span.Seconds:0} sec" : null,
            span.Milliseconds > 0 ? $"{span.Milliseconds:0} ms" : null
        }.Where(s => !string.IsNullOrWhiteSpace(s));

        return string.Join(", ", parts);
    }
}
