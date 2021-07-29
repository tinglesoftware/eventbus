using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Tingle.EventBus.Tests.InMemoryHarness
{
    class FakeHostEnvironment : IHostEnvironment
    {
        public string? EnvironmentName { get; set; }
        public string? ApplicationName { get; set; }
        public string? ContentRootPath { get; set; }
        public IFileProvider? ContentRootFileProvider { get; set; }
    }
}
