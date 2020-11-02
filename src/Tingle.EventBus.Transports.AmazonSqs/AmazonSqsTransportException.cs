using System;
using System.Runtime.Serialization;

namespace Tingle.EventBus.Transports.AmazonSqs
{
    [Serializable]
    public class AmazonSqsTransportException : Exception
    {
        public AmazonSqsTransportException() { }

        public AmazonSqsTransportException(string message) : base(message) { }

        public AmazonSqsTransportException(string message, Exception innerException) : base(message, innerException) { }

        protected AmazonSqsTransportException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
