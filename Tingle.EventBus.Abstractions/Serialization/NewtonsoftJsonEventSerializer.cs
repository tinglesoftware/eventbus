using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tingle.EventBus.Abstractions.Serialization
{
    public class NewtonsoftJsonEventSerializer : IEventSerializer
    {
        private readonly JsonSerializer serializer;
        private readonly JsonSerializerSettings settings;

        public NewtonsoftJsonEventSerializer(IOptions<EventBusOptions> optionsAccessor)
        {
            var options = optionsAccessor?.Value?.SerializerOptions ?? throw new ArgumentNullException(nameof(optionsAccessor));

            settings = new JsonSerializerSettings()
            {
                NullValueHandling = options.IgnoreNullValues ? NullValueHandling.Ignore : NullValueHandling.Include,
                Formatting = options.Indented ? Formatting.Indented : Formatting.None,
            };

            serializer = JsonSerializer.Create(settings);
        }

        public Task<TEvent> FromStreamAsync<TEvent>(MemoryStream stream, Encoding encoding, CancellationToken cancellationToken = default)
        {
            using (stream)
            {
                if (typeof(Stream).IsAssignableFrom(typeof(TEvent))) return Task.FromResult((TEvent)(object)stream);

                using var sr = new StreamReader(stream, encoding);
                using var jr = new JsonTextReader(sr);
                var result = serializer.Deserialize<TEvent>(jr);
                return Task.FromResult(result);
            }
        }

        public Task<object> FromStreamAsync(MemoryStream stream, Type type, Encoding encoding, CancellationToken cancellationToken = default)
        {
            using (stream)
            {
                if (typeof(Stream).IsAssignableFrom(type)) return Task.FromResult((object)stream);

                using var sr = new StreamReader(stream, encoding);
                using var jr = new JsonTextReader(sr);
                var result = serializer.Deserialize(jr, type);
                return Task.FromResult(result);
            }
        }

        public Task<MemoryStream> ToStreamAsync<TEvent>(TEvent @event, Encoding encoding, CancellationToken cancellationToken = default)
        {
            // !!!!!! --------------- WARNING --------------- !!!!!!
            // using the StreamWriter with the JsonTextWriter results in a byte array that cannot be used
            // with Newtonsoft.Json.JsonConvert.Deserialize(...)
            var json = JsonConvert.SerializeObject(@event, settings);
            var bytes = encoding.GetBytes(json);
            return Task.FromResult(new MemoryStream(bytes));
        }
    }
}
