using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A class to finish the configuration of instances of <see cref="XmlEventSerializerOptions"/>.
/// </summary>
internal class XmlEventSerializerConfigureOptions : IValidateOptions<XmlEventSerializerOptions>
{
    public ValidateOptionsResult Validate(string name, XmlEventSerializerOptions options)
    {
        return ValidateOptionsResult.Success;
    }
}
