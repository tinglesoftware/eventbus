using Microsoft.Extensions.Options;
using System;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// A class to finish the configuration of instances of <see cref="InMemoryTestHarnessOptions"/>.
    /// </summary>
    internal class InMemoryTestHarnessPostConfigureOptions : IPostConfigureOptions<InMemoryTestHarnessOptions>
    {
        private readonly InMemoryTransportOptions transportOptions;

        public InMemoryTestHarnessPostConfigureOptions(IOptions<InMemoryTransportOptions> transportOptionsAccessor)
        {
            transportOptions = transportOptionsAccessor?.Value ?? throw new ArgumentNullException(nameof(transportOptionsAccessor));
        }

        public void PostConfigure(string name, InMemoryTestHarnessOptions options)
        {
            var ticks = transportOptions.DeliveryDelay?.Ticks ?? 0;
            ticks = Math.Max(0, ticks); // should not be less than zero

            // Add 50ms from the delivery delay
            options.DefaultDelay = TimeSpan.FromTicks(ticks) + TimeSpan.FromSeconds(0.05f);
        }
    }
}
