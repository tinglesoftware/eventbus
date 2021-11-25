using System.Net;
using Tingle.EventBus.Transports.Amazon;

namespace Amazon.Runtime;

/// <summary>
/// Extension methods for <see cref="AmazonWebServiceResponse "/>.
/// </summary>
public static class AmazonWebServiceResponseExtensions
{
    /// <summary>
    /// Checks if the request was successful.
    /// </summary>
    /// <param name="response">The <see cref="AmazonWebServiceResponse"/> to check.</param>
    /// <returns></returns>
    public static bool Successful(this AmazonWebServiceResponse response)
    {
        var statusCode = response.HttpStatusCode;
        return statusCode >= HttpStatusCode.OK && statusCode < HttpStatusCode.MultipleChoices;
    }

    /// <summary>
    /// Ensure the request was successful.
    /// </summary>
    /// <param name="response">The <see cref="AmazonWebServiceResponse"/> to check.</param>
    /// <exception cref="AmazonSqsTransportException">If the request was not successful.</exception>
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
