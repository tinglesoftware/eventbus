using Microsoft.Extensions.Hosting;
using System.Text.RegularExpressions;
using Tingle.EventBus.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Specifies options for naming behaviour and requirements.
/// </summary>
public partial class EventBusNamingOptions
{
    private const string TrimPattern = "(Event|Consumer|EventConsumer)$";
    private const string NamePattern = "(?<=[a-z0-9])[A-Z]";
    private const string ReplacePatternKebabCase = "[^a-zA-Z0-9-]";
    private const string ReplacePatternSnakeCase = "[^a-zA-Z0-9_]";
    private const string ReplacePatternDotCase = "[^a-zA-Z0-9\\.]";
    private const string ReplacePatternDefault = "[^a-zA-Z0-9-_\\.]";

    private static readonly Regex trimPattern = GetTrimPattern();
    private static readonly Regex namePattern = GetNamePattern();
    private static readonly Regex replacePatternKebabCase = GetReplacePatternKebabCase();
    private static readonly Regex replacePatternSnakeCase = GetReplacePatternSnakeCase();
    private static readonly Regex replacePatternDotCase = GetReplacePatternDotCase();
    private static readonly Regex replacePatternDefault = GetReplacePatternDefault();

    /// <summary>
    /// The scope to use for queues and subscriptions.
    /// Set to <see langword="null"/> to disable scoping of entities.
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>
    /// The naming convention to use when generating names for types and entities on the selected transport.
    /// Defaults to <see cref="NamingConvention.KebabCase"/>.
    /// </summary>
    public NamingConvention Convention { get; set; } = NamingConvention.KebabCase;

    /// <summary>
    /// Determines if to trim suffixes such as <c>Consumer</c>, <c>Event</c> and <c>EventConsumer</c>
    /// in names generated from type names.
    /// For example <c>DoorOpenedEvent</c> would be trimmed to <c>DoorOpened</c>,
    /// <c>DoorOpenedEventConsumer</c> would be trimmed to <c>DoorOpened</c>,
    /// <c>DoorOpenedConsumer</c> would be trimmed to <c>DoorOpened</c>.
    /// Defaults to <see langword="true"/>
    /// </summary>
    public bool TrimTypeNames { get; set; } = true;

    /// <summary>
    /// Determines if to use the full name when generating entity names.
    /// This should always be enabled if there are types with the same names.
    /// For example <see cref="string"/> would produce <c>System.String</c>, <c>system-string</c>,
    /// or <c>system_string</c>; when enabled, otherwise just <c>string</c>.
    /// Defaults to <see langword="true"/>
    /// </summary>
    public bool UseFullTypeNames { get; set; } = true;

    /// <summary>
    /// The source used to generate names for consumers.
    /// Some transports are very sensitive to the value used here and thus should be used carefully.
    /// When set to <see cref="ConsumerNameSource.Prefix"/>, each subscription/exchange will
    /// be named the same as <see cref="ConsumerNamePrefix"/> before appending the event name.
    /// For applications with more than one consumer per event type, use <see cref="ConsumerNameSource.TypeName"/>
    /// to avoid duplicates.
    /// Defaults to <see cref="ConsumerNameSource.TypeName"/>.
    /// </summary>
    public ConsumerNameSource ConsumerNameSource { get; set; } = ConsumerNameSource.TypeName;

    /// <summary>
    /// The prefix used with <see cref="ConsumerNameSource.Prefix"/> and <see cref="ConsumerNameSource.PrefixAndTypeName"/>.
    /// Defaults to <see cref="IHostEnvironment.ApplicationName"/>.
    /// </summary>
    public string? ConsumerNamePrefix { get; set; }

    /// <summary>
    /// Gets the application name from <see cref="IHostEnvironment.ApplicationName"/>
    /// and applies the naming settings in this options.
    /// </summary>
    /// <param name="environment"></param>
    /// <returns></returns>
    public string GetApplicationName(IHostEnvironment environment)
    {
        if (environment is null) throw new ArgumentNullException(nameof(environment));

        var name = environment.ApplicationName;
        name = ApplyNamingConvention(name);
        name = AppendScope(name);
        name = ReplaceInvalidCharacters(name);
        return name;
    }

    internal string TrimCommonSuffixes(string untrimmed) => TrimTypeNames ? trimPattern.Replace(untrimmed, "") : untrimmed;

    internal string ApplyNamingConvention(string raw)
    {
        return Convention switch
        {
            NamingConvention.KebabCase => namePattern.Replace(raw, m => "-" + m.Value).ToLowerInvariant(),
            NamingConvention.SnakeCase => namePattern.Replace(raw, m => "_" + m.Value).ToLowerInvariant(),
            NamingConvention.DotCase => namePattern.Replace(raw, m => "." + m.Value).ToLowerInvariant(),
            _ => raw,
        };
    }

    internal string ReplaceInvalidCharacters(string raw)
    {
        return Convention switch
        {
            NamingConvention.KebabCase => replacePatternKebabCase.Replace(raw, "-"),
            NamingConvention.SnakeCase => replacePatternSnakeCase.Replace(raw, "_"),
            NamingConvention.DotCase => replacePatternDotCase.Replace(raw, "."),
            _ => replacePatternDefault.Replace(raw, ""),
        };
    }

    internal string AppendScope(string unscoped) => string.IsNullOrWhiteSpace(Scope) ? unscoped : Join(Scope, unscoped);

    /// <summary>
    /// Concatenates all the elements of a string array,
    /// using a separator defined by <see cref="Convention"/> between each element in lowercase.
    /// </summary>
    /// <param name="values">An array that contains the elements to concatenate.</param>
    /// <returns>
    /// A lowercase string that consists of the elements in <paramref name="values"/> delimited by a separator string
    /// defined by <see cref="Convention"/>.
    /// -or- <see cref="string.Empty"/> if <paramref name="values"/> has zero elements.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// The length of the resulting string overflows the maximum allowed length (<see cref="int.MaxValue"/>).
    /// </exception>
    public string Join(params string[] values)
    {
        if (values is null) throw new ArgumentNullException(nameof(values));

        // remove nulls
        values = values.Where(a => !string.IsNullOrWhiteSpace(a)).ToArray();

        return (Convention switch
        {
            NamingConvention.KebabCase => string.Join("-", values),
            NamingConvention.SnakeCase => string.Join("_", values),
            NamingConvention.DotCase => string.Join(".", values),
            _ => throw new InvalidOperationException($"'{nameof(NamingConvention)}.{Convention}' does not support joining"),
        }).ToLowerInvariant();
    }

#if NET7_0_OR_GREATER
    [GeneratedRegex(TrimPattern, RegexOptions.Compiled)]
    private static partial Regex GetTrimPattern();
    [GeneratedRegex(NamePattern, RegexOptions.Compiled)]
    private static partial Regex GetNamePattern();
    [GeneratedRegex(ReplacePatternKebabCase, RegexOptions.Compiled)]
    private static partial Regex GetReplacePatternKebabCase();
    [GeneratedRegex(ReplacePatternSnakeCase, RegexOptions.Compiled)]
    private static partial Regex GetReplacePatternSnakeCase();
    [GeneratedRegex(ReplacePatternDotCase, RegexOptions.Compiled)]
    private static partial Regex GetReplacePatternDotCase();
    [GeneratedRegex(ReplacePatternDefault, RegexOptions.Compiled)]
    private static partial Regex GetReplacePatternDefault();
#else
    private static Regex GetTrimPattern() => new(TrimPattern, RegexOptions.Compiled);
    private static Regex GetNamePattern() => new(NamePattern, RegexOptions.Compiled);
    private static Regex GetReplacePatternKebabCase() => new(ReplacePatternKebabCase, RegexOptions.Compiled);
    private static Regex GetReplacePatternSnakeCase() => new(ReplacePatternSnakeCase, RegexOptions.Compiled);
    private static Regex GetReplacePatternDotCase() => new(ReplacePatternDotCase, RegexOptions.Compiled);
    private static Regex GetReplacePatternDefault() => new(ReplacePatternDefault, RegexOptions.Compiled);
#endif
}
