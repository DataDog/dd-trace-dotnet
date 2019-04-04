#if !NETSTANDARD2_0

using System.ServiceModel.Description;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <inheritdoc />
    public class WcfEndpointBehavior : IEndpointBehavior
    {
        /// <inheritdoc />
        public void AddBindingParameters(ServiceEndpoint endpoint, System.ServiceModel.Channels.BindingParameterCollection bindingParameters)
        {
        }

        /// <inheritdoc />
        public void ApplyClientBehavior(ServiceEndpoint endpoint, System.ServiceModel.Dispatcher.ClientRuntime clientRuntime)
        {
        }

        /// <inheritdoc />
        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, System.ServiceModel.Dispatcher.EndpointDispatcher endpointDispatcher)
        {
            var inspector = new WcfDispatchMessageInspector();
            endpointDispatcher.DispatchRuntime.MessageInspectors.Add(inspector);
        }

        /// <inheritdoc />
        public void Validate(ServiceEndpoint endpoint)
        {
        }
    }
}

#endif
