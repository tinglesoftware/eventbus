using System;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Specifies options for readniess checks.
    /// </summary>
    public class EventBusReadinessOptions
    {
        /// <summary>
        /// The duration of time that the bus can wait for readiness before timing out.
        /// The default value is 5 minutes. Max value is 15 minutes and minimum is 5 seconds.
        /// </summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
    }
}
