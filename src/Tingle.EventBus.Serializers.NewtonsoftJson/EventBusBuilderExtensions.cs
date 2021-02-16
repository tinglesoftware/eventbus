using Microsoft.Extensions.Options;
using System;
using Tingle.EventBus.Serializers;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods on <see cref="EventBusBuilder"/> for NewtonsoftJson serializer.
    /// </summary>
    public static class EventBusBuilderExtensions
    {
        /// <summary>
        /// Add NewtonsoftJson serializer to the EventBus.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public static EventBusBuilder AddNewtonsoftJsonSerializer(this EventBusBuilder builder,
                                                                  Action<NewtonsoftJsonSerializerOptions> configure = null)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            // Configure the options for the serializer
            var services = builder.Services;
            if (configure != null) services.Configure(configure);
            services.AddSingleton<IPostConfigureOptions<NewtonsoftJsonSerializerOptions>, NewtonsoftJsonSerializerPostConfigureOptions>();

            // Register the serializer
            services.AddSingleton<NewtonsoftJsonSerializer>();

            return builder;
        }

        /// <summary>
        /// Use the included NewtonsoftJson serializer as the default.
        /// </summary>
        /// <returns></returns>
        public static EventBusBuilder UseDefaultNewtonsoftJsonSerializer(this EventBusBuilder builder)
        {
            return builder.UseDefaultSerializer<NewtonsoftJsonSerializer>();
        }
    }
}
