namespace CustomEventSerializer.Models;

public class AzureDevOpsEventResourceRepositoryProject
{
    /// <summary>
    /// The unique identifier of the project.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The name of the project.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The url for the project.
    /// </summary>
    /// <example>https://dev.azure.com/tingle/_apis/projects/cea8cb01-dd13-4588-b27a-55fa170e4e94</example>
    /// <remarks>Useful for extraction of the name and url of the organization.</remarks>
    public string? Url { get; set; }
}
