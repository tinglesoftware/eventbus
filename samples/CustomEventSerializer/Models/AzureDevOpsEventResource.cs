namespace CustomEventSerializer.Models;

public class AzureDevOpsEventResource
{
    /// <summary>
    /// List of updated references.
    /// </summary>
    public List<AzureDevOpsEventResourceRefUpdate>? RefUpdates { get; set; }

    /// <summary>
    /// Details about the repository.
    /// </summary>
    public AzureDevOpsEventResourceRepository? Repository { get; set; }
}
