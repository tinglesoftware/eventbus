using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Tingle.EventBus;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Specifies options for naming behaviour and requirements.
    /// </summary>
    public class EventBusNamingOptions
    {
        private static readonly Regex trimPattern = new("(Event|Consumer|EventConsumer)$", RegexOptions.Compiled);
        private static readonly Regex namePattern = new("(?<=[a-z0-9])[A-Z]", RegexOptions.Compiled);
        private static readonly Regex replacePatternKebabCase = new("[^a-zA-Z0-9-]", RegexOptions.Compiled);
        private static readonly Regex replacePatternSnakeCase = new("[^a-zA-Z0-9_]", RegexOptions.Compiled);
        private static readonly Regex replacePatternDotCase = new("[^a-zA-Z0-9\\.]", RegexOptions.Compiled);
        private static readonly Regex replacePatternDefault = new("[^a-zA-Z0-9-_\\.]", RegexOptions.Compiled);

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
        /// Indicates if the consumer name generated should be suffixed with the event name.
        /// Some transports require this value to be <see langword="true"/>.
        /// Setting to false can reduce the length of the consumer name hence being useful in scenarios
        /// where the transport allow same name for two consumers so long as they are for different events.
        /// Defaults to <see langword="true"/>
        /// </summary>
        public bool SuffixConsumerName { get; set; } = true;

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

        internal string Join(params string[] args)
        {
            if (args is null) throw new ArgumentNullException(nameof(args));

            // remove nulls
            args = args.Where(a => !string.IsNullOrWhiteSpace(a)).ToArray();

            return Convention switch
            {
                NamingConvention.KebabCase => string.Join("-", args).ToLowerInvariant(),
                NamingConvention.SnakeCase => string.Join("_", args).ToLowerInvariant(),
                NamingConvention.DotCase => string.Join(".", args).ToLowerInvariant(),
                _ => throw new ArgumentOutOfRangeException(nameof(Convention), $"'{Convention}' does not support joining"),
            };
        }
    }
}
