using System.Net;
using Tingle.EventBus.Transports.Amazon.Sqs;

namespace Amazon.Runtime
{
    /// <summary>
    /// Extension methods for <see cref="AmazonWebServiceResponse "/>.
    /// </summary>
    internal static class AmazonWebServiceResponseExtensions
    {
        public static bool Successful(this AmazonWebServiceResponse response)
        {
            var statusCode = response.HttpStatusCode;
            return statusCode >= HttpStatusCode.OK && statusCode < HttpStatusCode.MultipleChoices;
        }

        public static void EnsureSuccess(this AmazonWebServiceResponse response)
        {
            if (response.Successful()) return;
            const string documentationUri = "https://aws.amazon.com/blogs/developer/logging-with-the-aws-sdk-for-net/";

            var statusCode = response.HttpStatusCode;
            var requestId = response.ResponseMetadata.RequestId;

            throw new AmazonSqsTransportException($"Received unsuccessful response ({statusCode}) from AWS endpoint.\n" +
                                                  $"See AWS SDK logs ({requestId}) for more details: {documentationUri}");
        }
    }
}
