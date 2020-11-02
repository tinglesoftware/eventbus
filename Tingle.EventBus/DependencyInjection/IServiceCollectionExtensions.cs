using System;
using Tingle.EventBus.Abstractions;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods on <see cref="IServiceCollection"/> for EventBus.
    /// </summary>
    public static class IServiceCollectionExtensions
    {
        /// <summary>
        /// Add Event bus services.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> instance to add services to.</param>
        /// <param name="setupAction">An optional action for setting up the bus.</param>
        /// <returns></returns>
        public static IServiceCollection AddEventBus(this IServiceCollection services, Action<EventBusBuilder> setupAction = null)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            var builder = new EventBusBuilder(services)
                .UseNewtonsoftJsonSerializer();

            // register services that can resolve each other
            services.AddHostedService(p => p.GetRequiredService<IEventBus>());
            services.AddTransient<IEventBusPublisher, EventBusPublisher>();

            setupAction?.Invoke(builder);

            return services;
        }
    }
}
