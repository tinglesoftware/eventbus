﻿using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Tingle.EventBus;
using Tingle.EventBus.Configuration;
using Tingle.EventBus.Serialization;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A class to finish configure non-trivial defaults of instances of <see cref="EventBusOptions"/>.
/// </summary>
internal class EventBusConfigureOptions(IHostEnvironment environment, IEnumerable<IEventBusConfigurator> configurators) : IConfigureOptions<EventBusOptions>,
                                                                                                                          IConfigureOptions<EventBusSerializationOptions>,
                                                                                                                          IPostConfigureOptions<EventBusOptions>,
                                                                                                                          IValidateOptions<EventBusOptions>,
                                                                                                                          IValidateOptions<EventBusSerializationOptions>
{
    /// <inheritdoc/>
    public void Configure(EventBusOptions options)
    {
        foreach (var cfg in configurators.Reverse())
        {
            cfg.Configure(options);
        }

        // Set the default ConsumerNamePrefix
        options.Naming.ConsumerNamePrefix ??= environment.ApplicationName;
    }

    /// <inheritdoc/>
    public void Configure(EventBusSerializationOptions options)
    {
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

    /// <inheritdoc/>
    public void PostConfigure(string? name, EventBusOptions options)
    {
        // Ensure there is at least one registered transport
        if (options.TransportMap.Count == 0)
        {
            throw new InvalidOperationException("There must be at least one registered transport.");
        }

        // If the default transport name has been set, ensure it is registered
        if (!string.IsNullOrWhiteSpace(options.DefaultTransportName))
        {
            // ensure the transport name set has been registered
            var tName = options.DefaultTransportName;
            if (!options.TransportMap.ContainsKey(tName))
            {
                throw new InvalidOperationException($"The default transport  specified '{tName}' must be a registered one.");
            }
        }

        // If the default transport name has not been set, and there is only one registered, set it as default
        if (string.IsNullOrWhiteSpace(options.DefaultTransportName))
        {
            if (options.TransportMap.Count == 1)
            {
                options.DefaultTransportName = options.TransportMap.Keys.Single();
            }
        }

        // Configure each event and its consumers
        var registrations = options.Registrations.Values.ToList();
        foreach (var evr in registrations)
        {
            foreach (var cfg in configurators.Reverse())
            {
                cfg.Configure(evr, options);
            }
        }
    }

    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, EventBusOptions options)
    {
        // Ensure there are no events with the same name
        var registrations = options.Registrations.Values.ToList();
        var conflicted = registrations.GroupBy(r => r.EventName).FirstOrDefault(kvp => kvp.Count() > 1);
        if (conflicted != null)
        {
            var names = conflicted.Select(r => r.EventType.FullName);
            return ValidateOptionsResult.Fail($"The event name '{conflicted.Key}' cannot be used more than once."
                                            + $" Types:\r\n- {string.Join("\r\n- ", names)}");
        }

        // Ensure there are no consumers with the same name per event
        foreach (var evr in registrations)
        {
            var conflict = evr.Consumers.GroupBy(ecr => (ecr.ConsumerName, ecr.Deadletter)).FirstOrDefault(kvp => kvp.Count() > 1);
            if (conflict != null)
            {
                var names = conflict.Select(r => r.ConsumerType.FullName);
                var id = conflict.Key.ConsumerName;
                if (conflict.Key.Deadletter) id += " [dead-letter]";
                return ValidateOptionsResult.Fail($"The consumer name '({id})' cannot be used more than once on '{evr.EventType.Name}'."
                                                + $" Types:\r\n- {string.Join("\r\n- ", names)}");
            }
        }

        return ValidateOptionsResult.Success;
    }

    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, EventBusSerializationOptions options)
    {
        // Ensure we have SerializerOptions set
        if (options.SerializerOptions == null)
        {
            return ValidateOptionsResult.Fail($"'{nameof(options.SerializerOptions)}' must be set.");
        }

        if (options.SerializerOptions.TypeInfoResolverChain.Count == 0 || options.SerializerOptions.TypeInfoResolver is null)
        {
            if (System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault)
            {
                // These ignores will help complete the build. However, trimming/AoT will not fail due to this
#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
#pragma warning disable IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
                options.SerializerOptions.TypeInfoResolverChain.Add(
                    new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver());
#pragma warning restore IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
#pragma warning restore IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
            }
            else
            {
                return ValidateOptionsResult.Fail(
                    $"'{nameof(options.SerializerOptions.TypeInfoResolver)}' is null and '{nameof(options.SerializerOptions.TypeInfoResolverChain)}' is empty." +
                    " When using source generation make sure to set one of them");
            }
        }

        // Ensure we have HostInfo set
        if (options.HostInfo == null)
        {
            return ValidateOptionsResult.Fail($"'{nameof(options.HostInfo)}' must be set.");
        }

        return ValidateOptionsResult.Success;
    }
}
