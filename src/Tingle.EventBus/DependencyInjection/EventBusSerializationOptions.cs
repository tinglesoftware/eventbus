using System.Text.Json;
using Tingle.EventBus.Serialization;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Specified options for serialization.
/// </summary>
public class EventBusSerializationOptions
{
    /// <summary>
    /// The options to use for serialization.
    /// </summary>
    public JsonSerializerOptions SerializerOptions { get; set; } = new JsonSerializerOptions
    {
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
                       | System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
        WriteIndented = false, // less data used
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault
                               | System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,

        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>
    /// The information about the host where the EventBus is running.
    /// </summary>
    public HostInfo? HostInfo { get; set; }
}
