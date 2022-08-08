using Microsoft.Extensions.Options;
using Tingle.EventBus.Serializers;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A class to finish the configuration of instances of <see cref="NewtonsoftJsonSerializerOptions"/>.
/// </summary>
internal class NewtonsoftJsonSerializerConfigureOptions : IValidateOptions<NewtonsoftJsonSerializerOptions>
{
    public ValidateOptionsResult Validate(string name, NewtonsoftJsonSerializerOptions options)
    {
        // Ensure the settings are provided
        if (options.SerializerSettings == null)
        {
            return ValidateOptionsResult.Fail($"'{nameof(options.SerializerSettings)}' must be provided");
        }

        return ValidateOptionsResult.Success;
    }
}
