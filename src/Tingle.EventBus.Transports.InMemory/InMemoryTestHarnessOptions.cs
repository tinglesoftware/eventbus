using System;
using Tingle.EventBus.Transports.InMemory;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Configuration options for <see cref="InMemoryTestHarness"/>
/// </summary>
public class InMemoryTestHarnessOptions
{
    /// <summary>
    /// The duration of time to delay.
    /// Defaults to 50ms (0.05 sec).
    /// </summary>
    public TimeSpan DefaultDelay { get; set; } = TimeSpan.FromMilliseconds(50);
}
