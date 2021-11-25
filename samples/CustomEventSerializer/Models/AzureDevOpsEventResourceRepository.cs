using System;

namespace CustomEventSerializer.Models;

public class AzureDevOpsEventResourceRepository
{
    /// <summary>
    /// The unique identifier of the repository.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The name of the repository.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The details about the project which owns the repository.
    /// </summary>
    public AzureDevOpsEventResourceRepositoryProject? Project { get; set; }

    /// <summary>
    /// The default branch of the repository.
    /// </summary>
    public string? DefaultBranch { get; set; }
}
