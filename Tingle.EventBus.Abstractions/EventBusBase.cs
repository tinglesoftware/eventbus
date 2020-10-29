using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus.Abstractions
{
    public abstract class EventBusBase : IEventBus
    {
        protected static readonly DiagnosticListener DiagnosticListener = new DiagnosticListener("Tingle-EventBus");

        private static readonly Regex namePattern = new Regex("(?<=[a-z0-9])[A-Z]", RegexOptions.Compiled);

        private readonly ConcurrentDictionary<Type, string> typeNamesCache = new ConcurrentDictionary<Type, string>();
        private readonly ILogger logger;

        public EventBusBase(IHostEnvironment environment, IOptions<EventBusOptions> optionsAccessor, ILoggerFactory loggerFactory)
        {
            Options = optionsAccessor?.Value ?? throw new ArgumentNullException(nameof(optionsAccessor));
            logger = loggerFactory?.CreateLogger("EventBus") ?? throw new ArgumentNullException(nameof(logger));
            Environment = environment ?? throw new ArgumentNullException(nameof(environment));
        }

        protected IHostEnvironment Environment { get; }
        protected EventBusOptions Options { get; }

        /// <inheritdoc/>
        public abstract Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        public abstract Task<string> PublishAsync<TEvent>(EventContext<TEvent> @event, DateTimeOffset? scheduled = null, CancellationToken cancellationToken = default);

        /// <inheritdoc/>
        public abstract Task StartAsync(CancellationToken cancellationToken);

        /// <inheritdoc/>
        public abstract Task StopAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Get's the event name for a given type.
        /// </summary>
        /// <param name="type"></param>
        protected virtual string GetEventName(Type type) => typeNamesCache.GetOrAdd(type, CreateEventName(type));

        /// <summary>
        /// Get's the event name for a given type.
        /// </summary>
        /// <typeparam name="TEvent"></typeparam>
        /// <returns></returns>
        protected virtual string GetEventName<TEvent>() => GetEventName(typeof(TEvent));

        private string CreateEventName(Type type)
        {
            if (type is null) throw new ArgumentNullException(nameof(type));

            var name = Options.UseFullTypeNames ? type.FullName : type.Name;
            return ApplyNamingConvention(name, Options.NamingConvention);
        }

        /// <summary>
        /// Get's the consumer name for a given type.
        /// </summary>
        /// <param name="type"></param>
        protected virtual string GetConsumerName(Type type, bool forceConsumerName)
        {
            return typeNamesCache.GetOrAdd(type, CreateConsumerName(type, forceConsumerName));
        }

        private string CreateConsumerName(Type type, bool forceConsumerName)
        {
            if (type is null) throw new ArgumentNullException(nameof(type));

            // for consumers, we always enforce the full type name
            var name = (Options.UseApplicationNameInsteadOfConsumerName && !forceConsumerName)
                        ? Environment.ApplicationName
                        : type.FullName;
            return ApplyNamingConvention(name, Options.NamingConvention);
        }

        private static string ApplyNamingConvention(string name, EventBusNamingConvention convention)
        {
            return convention switch
            {
                EventBusNamingConvention.KebabCase => namePattern.Replace(name, m => "-" + m.Value).ToLowerInvariant(),
                EventBusNamingConvention.SnakeCase => namePattern.Replace(name, m => "_" + m.Value).ToLowerInvariant(),
                _ => name,
            };
        }
    }
}
