using System.Text;

namespace Confluent.Kafka
{
    internal static class KafkaExtensions
    {
        public static Headers AddIfNotNull(this Headers headers, string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                headers.Add(key, Encoding.UTF8.GetBytes(value));
            }
            return headers;
        }

        public static bool TryGetValue(this Headers headers, string key, out string? value)
        {
            if (headers.TryGetLastBytes(key, out var bytes))
            {
                value = Encoding.UTF8.GetString(bytes);
                return true;
            }
            value = null;
            return false;
        }
    }
}
