using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A class to finish the configuration of instances of <see cref="InMemoryTestHarnessOptions"/>.
/// </summary>
internal class InMemoryTestHarnessConfigureOptions : IPostConfigureOptions<InMemoryTestHarnessOptions>
{
    public void PostConfigure(string name, InMemoryTestHarnessOptions options)
    {
        // intentionally left bank for future use
    }
}
