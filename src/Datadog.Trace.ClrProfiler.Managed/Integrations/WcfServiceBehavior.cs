#if !NETSTANDARD2_0

using System.Collections.ObjectModel;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <inheritdoc />
    /// <summary>
    ///     IServiceBehavior used to trace within a WCF request
    /// </summary>
    public class WcfServiceBehavior : IServiceBehavior
    {
        /// <inheritdoc />
        public void Validate(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
        {
        }

        /// <inheritdoc />
        public void AddBindingParameters(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase, Collection<ServiceEndpoint> endpoints, BindingParameterCollection bindingParameters)
        {
        }

        /// <inheritdoc />
        public void ApplyDispatchBehavior(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
        {
            foreach (ChannelDispatcher chDisp in serviceHostBase.ChannelDispatchers)
            {
                foreach (var epDisp in chDisp.Endpoints)
                {
                    epDisp.DispatchRuntime.MessageInspectors.Add(new WcfDispatchMessageInspector());
                    // foreach (DispatchOperation op in epDisp.DispatchRuntime.Operations)
                    // {
                    //    op.ParameterInspectors.Add(new WcfDispatchMessageInspector());
                    // }
                }
            }
        }
    }
}

#endif
