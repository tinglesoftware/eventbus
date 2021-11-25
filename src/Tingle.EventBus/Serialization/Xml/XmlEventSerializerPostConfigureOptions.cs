using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A class to finish the configuration of instances of <see cref="XmlEventSerializerOptions"/>.
/// </summary>
internal class XmlEventSerializerPostConfigureOptions : IPostConfigureOptions<XmlEventSerializerOptions>
{
    public void PostConfigure(string name, XmlEventSerializerOptions options)
    {
        // intentionally left blank for future use
    }
}
