using System;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace Samples.Wcf.Client
{
    public class Startup
    {
        public static void InvokeCalculatorService(Binding binding, Uri baseAddress)
        {
            var calculatorServiceBaseAddress = new Uri(baseAddress, "CalculatorService");
            var address = new EndpointAddress(calculatorServiceBaseAddress);
            using (var calculator = new CalculatorClient(binding, address))
            {
                // Add the CustomEndpointBehavior / ClientMessageInspector to add headers on calls to the service
                calculator.ChannelFactory.Endpoint.EndpointBehaviors.Add(new CustomEndpointBehavior());
                double result = calculator.Add(1, 2);
                Console.WriteLine($"[Client] Result: {result}");
            }
        }
    }
}
