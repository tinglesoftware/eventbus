using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Tingle.EventBus;
using Tingle.EventBus.Serialization;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A class to finish configure non-trivial defaults of instances of <see cref="EventBusOptions"/>.
/// </summary>
internal class EventBusConfigureOptions : IConfigureOptions<EventBusOptions>
{
    private readonly IHostEnvironment environment;

    public EventBusConfigureOptions(IHostEnvironment environment)
    {
        this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    /// <inheritdoc/>
    public void Configure(EventBusOptions options)
    {
        // Set the default ConsumerNamePrefix
        options.Naming.ConsumerNamePrefix ??= environment.ApplicationName;

        // Setup HostInfo
        if (options.HostInfo == null)
        {
            var entry = System.Reflection.Assembly.GetEntryAssembly() ?? System.Reflection.Assembly.GetCallingAssembly();
            options.HostInfo = new HostInfo
            {
                ApplicationName = environment.ApplicationName,
                ApplicationVersion = entry.GetName().Version?.ToString(),
                EnvironmentName = environment.EnvironmentName,
                LibraryVersion = typeof(EventBus).Assembly.GetName().Version?.ToString(),
                MachineName = Environment.MachineName,
                OperatingSystem = Environment.OSVersion.ToString(),
            };
        }
    }
}
