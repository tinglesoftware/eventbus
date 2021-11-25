using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Tingle.EventBus.Serializers;

/// <summary>
/// Options for configuring NewtonsoftJson based serializer.
/// </summary>
public class NewtonsoftJsonSerializerOptions
{
    /// <summary>
    /// The options to use for serialization.
    /// </summary>
    public JsonSerializerSettings SerializerSettings { get; set; } = new JsonSerializerSettings
    {
        Formatting = Formatting.None, // less data used
        NullValueHandling = NullValueHandling.Ignore,

        ContractResolver = new CamelCasePropertyNamesContractResolver(),
    };
}
