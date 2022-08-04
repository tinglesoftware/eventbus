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
        }.Where(s => !string.IsNullOrWhiteSpace(s));

        return string.Join(", ", parts);
    }
}
