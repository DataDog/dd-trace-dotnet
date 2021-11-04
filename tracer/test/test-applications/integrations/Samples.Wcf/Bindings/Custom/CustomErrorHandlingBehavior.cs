using System.Collections.ObjectModel;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;

namespace Samples.Wcf.Bindings.Custom
{
    public class CustomErrorHandlingBehavior : IServiceBehavior, IEndpointBehavior
    {
        //// Begin IServiceBehavior interface methods

        public void AddBindingParameters(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase, Collection<ServiceEndpoint> endpoints, BindingParameterCollection bindingParameters)
        {
        }

        public void ApplyDispatchBehavior(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
        {
            var errorHandler = new CustomErrorHandler();

            foreach (var dispatcher in serviceHostBase.ChannelDispatchers)
            {
                var channelDispatcher = dispatcher as ChannelDispatcher;
                channelDispatcher.ErrorHandlers.Add(errorHandler);
            }
        }

        public void Validate(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
        {
        }

        //// End IServiceBehavior interface methods

        //// Begin IEndpointBehavior interface methods
        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
        {
        }

        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
            clientRuntime?.CallbackDispatchRuntime.ChannelDispatcher.ErrorHandlers.Add(new CustomErrorHandler());
        }

        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
        {
            endpointDispatcher?.ChannelDispatcher.ErrorHandlers.Add(new CustomErrorHandler());
        }

        public void Validate(ServiceEndpoint endpoint)
        {
        }
        //// End IEndpointBehavior interface methods
    }
}
