namespace System
{
    /// <summary>
    /// Extension methods for <see cref="TimeSpan"/>.
    /// </summary>
    public static class TimeSpanExtensions
    {
        /// <summary>
        /// Generates a reable string.
        /// </summary>
        /// <param name="span"></param>
        /// <returns></returns>
        public static string ToReadableString(this TimeSpan span)
        {
            string formatted = string.Format("{0}{1}{2}{3}",
                                             (span.Days / 7) > 0 ? $"{(span.Days / 7):0} weeks, " : string.Empty,
                                             span.Days % 7 > 0 ? $"{(span.Days % 7):0} days, " : string.Empty,
                                             span.Hours > 0 ? $"{span.Hours:0} hours, " : string.Empty,
                                             span.Minutes > 0 ? $"{span.Minutes:0} min, " : string.Empty);

            if (formatted.EndsWith(", ")) formatted = formatted[0..^2];
            return formatted;
        }
    }
}
