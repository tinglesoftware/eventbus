using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Tingle.EventBus.Serializers
{
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
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,

            ContractResolver = new CamelCasePropertyNamesContractResolver(),
        };
    }
}
