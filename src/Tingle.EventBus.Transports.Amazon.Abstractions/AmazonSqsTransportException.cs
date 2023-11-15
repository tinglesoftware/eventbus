namespace Tingle.EventBus.Transports.Amazon;

///
[Serializable]
public class AmazonSqsTransportException : Exception
{
    ///
    public AmazonSqsTransportException() { }

    ///
    public AmazonSqsTransportException(string message) : base(message) { }

    ///
    public AmazonSqsTransportException(string message, Exception innerException) : base(message, innerException) { }
}
