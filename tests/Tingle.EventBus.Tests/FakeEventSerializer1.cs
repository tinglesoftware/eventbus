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
        /// <inheritdoc/>
        public Task<EventContext<T>?> DeserializeAsync<T>(Stream stream,
                                                          ContentType? contentType,
                                                          CancellationToken cancellationToken = default) where T : class
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task SerializeAsync<T>(Stream stream,
                                      EventContext<T> context,
                                      CancellationToken cancellationToken = default) where T : class
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task<MessageEnvelope<T>?> Deserialize2Async<T>(Stream stream,
                                                              ContentType? contentType,
                                                              CancellationToken cancellationToken = default) where T : class
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task SerializeAsync(Stream stream,
                                   MessageEnvelope envelope,
                                   CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
