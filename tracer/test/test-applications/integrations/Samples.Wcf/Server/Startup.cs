using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using Samples.Wcf.Bindings.Custom;

namespace Samples.Wcf.Server
{
    public class Startup
    {
        public static ServiceHost CreateCalculatorService(Binding binding, Uri baseAddress)
        {
            var selfHost = new ServiceHost(typeof(CalculatorService), baseAddress);
            selfHost.Description.Behaviors.Add(new CustomErrorHandlingBehavior());
            selfHost.Description.Behaviors.Remove(typeof(ServiceDebugBehavior)); // Remove default behavior
            selfHost.Description.Behaviors.Add(new ServiceDebugBehavior { IncludeExceptionDetailInFaults = true }); // Allow exceptions to show all details
            var serviceEndpoint = selfHost.AddServiceEndpoint(typeof(ICalculator), binding, "CalculatorService");
            
            // Adds the server DispatchMessageInspector
            serviceEndpoint.EndpointBehaviors.Add(new CustomEndpointBehavior());

            if (binding is not NetTcpBinding)
            {
                var smb = new ServiceMetadataBehavior { HttpGetEnabled = true };
                selfHost.Description.Behaviors.Add(smb);
            }

            selfHost.Opening += (sender, eventArgs) => Console.Write("Opening... ");
            selfHost.Opened += (sender, eventArgs) => LoggingHelper.WriteLineWithDate("done.");
            selfHost.Closing += (sender, eventArgs) => Console.Write("Closing... ");
            selfHost.Closed += (sender, eventArgs) => LoggingHelper.WriteLineWithDate("done.");
            selfHost.Faulted += (sender, eventArgs) => LoggingHelper.WriteLineWithDate("Faulted.");
            selfHost.UnknownMessageReceived += (sender, eventArgs) => LoggingHelper.WriteLineWithDate($"UnknownMessageReceived: {eventArgs.Message}");

            return selfHost;
        }
    }
}
