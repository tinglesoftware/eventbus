using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Tingle.EventBus.Tests;

public class FakeHostEnvironment : IHostEnvironment
{
    public FakeHostEnvironment() { } // Required for DI

    public FakeHostEnvironment(string applicationName) => ApplicationName = applicationName;

    public string EnvironmentName { get; set; } = default!;
    public string ApplicationName { get; set; } = default!;
    public string ContentRootPath { get; set; } = default!;
    public IFileProvider ContentRootFileProvider { get; set; } = default!;
}
