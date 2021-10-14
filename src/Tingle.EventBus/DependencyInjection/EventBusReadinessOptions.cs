using System;
using Tingle.EventBus.Readiness;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Specifies options for readiness checks.
    /// </summary>
    public class EventBusReadinessOptions
    {
        /// <summary>
        /// Whether to actually check for health or to just return <see langword="true"/>.
        /// This is useful for situations where the readiness check is not needed.
        /// Defaults to <see langword="false"/>.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// The duration of time that the bus can wait for readiness before timing out.
        /// The default value is 5 minutes. Max value is 15 minutes and minimum is 5 seconds.
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
    }
}
