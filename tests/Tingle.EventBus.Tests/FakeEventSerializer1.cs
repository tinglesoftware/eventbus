using System;
using System.IO;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Tingle.EventBus.Serialization;

namespace Tingle.EventBus.Tests
{
    internal class FakeEventSerializer1 : IEventSerializer
    {
        public Task<EventContext<T>?> DeserializeAsync<T>(Stream stream,
                                                          ContentType? contentType,
                                                          CancellationToken cancellationToken = default) where T : class
        {
            throw new NotImplementedException();
        }

        public Task SerializeAsync<T>(Stream stream,
                                      EventContext<T> context,
                                      HostInfo? hostInfo,
                                      CancellationToken cancellationToken = default) where T : class
        {
            throw new NotImplementedException();
        }
    }
}
