using Microsoft.Extensions.Options;
using Tingle.EventBus.Serializers;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A class to finish the configuration of instances of <see cref="NewtonsoftJsonSerializerOptions"/>.
/// </summary>
internal class NewtonsoftJsonSerializerPostConfigureOptions : IPostConfigureOptions<NewtonsoftJsonSerializerOptions>
{
    public void PostConfigure(string name, NewtonsoftJsonSerializerOptions options)
    {
        // Ensure the settings are provided
        if (options.SerializerSettings == null)
        {
            throw new InvalidOperationException($"'{nameof(options.SerializerSettings)}' must be provided");
        }
    }
}
