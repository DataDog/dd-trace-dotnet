// Sources referenced:
// - https://docs.microsoft.com/en-us/archive/blogs/mohamedg/adding-http-headers-to-wcf-calls
// - https://stackoverflow.com/questions/964433/how-to-add-a-custom-http-header-to-every-wcf-call

using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;

namespace Samples.Wcf.Client
{
    /// <summary>
    /// Represents a message inspector object that can be added to the <c>MessageInspectors</c> collection to view or modify messages.
    /// This is used to add custom HTTP headers to HTTP-based WCF endpoints
    /// </summary>
    public class ClientMessageInspector : IClientMessageInspector
    {
        /// <summary>
        /// Enables inspection or modification of a message before a request message is sent to a service.
        /// </summary>
        /// <param name="request">The message to be sent to the service.</param>
        /// <param name="channel">The WCF client object channel.</param>
        /// <returns>
        /// The object that is returned as the <paramref name="correlationState " /> argument of
        /// the <see cref="M:System.ServiceModel.Dispatcher.IClientMessageInspector.AfterReceiveReply(System.ServiceModel.Channels.Message@,System.Object)" /> method.
        /// This is null if no correlation state is used.The best practice is to make this a <see cref="T:System.Guid" /> to ensure that no two
        /// <paramref name="correlationState" /> objects are the same.
        /// </returns>
        public object BeforeSendRequest(ref Message request, IClientChannel channel)
        {
            HttpRequestMessageProperty httpRequestMessage;
            object httpRequestMessageObject;

            if (request.Properties.TryGetValue(HttpRequestMessageProperty.Name, out httpRequestMessageObject))
            {
                httpRequestMessage = httpRequestMessageObject as HttpRequestMessageProperty;
                if (string.IsNullOrEmpty(httpRequestMessage.Headers["upstream-service"]))
                {
                    httpRequestMessage.Headers["upstream-service"] = "Samples.Wcf.Client";
                }
            }
            else
            {
                httpRequestMessage = new HttpRequestMessageProperty();
                httpRequestMessage.Headers["upstream-service"] = "Samples.Wcf.Client";
                request.Properties.Add(HttpRequestMessageProperty.Name, httpRequestMessage);
            }

            return null;
        }

        /// <summary>
        /// Enables inspection or modification of a message after a reply message is received but prior to passing it back to the client application.
        /// </summary>
        /// <param name="reply">The message to be transformed into types and handed back to the client application.</param>
        /// <param name="correlationState">Correlation state data.</param>
        public void AfterReceiveReply(ref System.ServiceModel.Channels.Message reply, object correlationState)
        {
            // Nothing special here
        }
    }
}
