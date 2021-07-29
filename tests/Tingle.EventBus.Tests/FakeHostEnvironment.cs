using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Tingle.EventBus.Tests
{
    internal class FakeHostEnvironment : IHostEnvironment
    {
        public FakeHostEnvironment(string applicationName) => ApplicationName = applicationName;

        public string? EnvironmentName { get; set; }
        public string ApplicationName { get; set; }
        public string? ContentRootPath { get; set; }
        public IFileProvider? ContentRootFileProvider { get; set; }
    }
}
