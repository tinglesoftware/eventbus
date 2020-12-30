using System;
using Tingle.EventBus;

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
                .UseDefaultSerializer();

            // register necessary services
            services.AddTransient<IEventPublisher, EventPublisher>();
            services.AddSingleton<EventBus>();
            services.AddHostedService(p => p.GetRequiredService<EventBus>());

            setupAction?.Invoke(builder);

            return services;
        }
    }
}
