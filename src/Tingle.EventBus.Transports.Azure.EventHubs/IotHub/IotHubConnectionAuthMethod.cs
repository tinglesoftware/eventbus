using System.Text.Json.Serialization;

namespace Tingle.EventBus.Transports.Azure.EventHubs.IotHub;

/// <summary>
/// Model representing the data found in <c>iothub-connection-auth-method</c> property.
/// </summary>
/// <example>
/// {
///     "scope": "hub",
///     "type": "sas",
///     "issuer": "iothub",
///     "acceptingIpFilterRule": null
/// }
/// </example>
public record IotHubConnectionAuthMethod
{
    /// <example>hub</example>
    public virtual string? Scope { get; set; }

    /// <example>sas</example>
    public virtual string? Type { get; set; }

    /// <example>iothub</example>
    public virtual string? Issuer { get; set; }

    ///
    [JsonExtensionData]
    public Dictionary<string, object>? Extras { get; set; }
}
