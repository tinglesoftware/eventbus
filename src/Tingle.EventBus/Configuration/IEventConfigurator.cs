using Microsoft.Extensions.DependencyInjection;

namespace Tingle.EventBus.Configuration
{
    /// <summary>
    /// Abstraction for configuring instances of <see cref="EventRegistration"/>.
    /// </summary>
    public interface IEventConfigurator
    {
        /// <summary>
        /// Configure an instance of <see cref="EventRegistration"/>.
        /// This is called once for each event either during startup
        /// (when there is a consumer) or on first publish.
        /// </summary>
        /// <param name="registration"></param>
        /// <param name="options"></param>
        void Configure(EventRegistration registration, EventBusOptions options);
    }
}
