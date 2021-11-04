using System;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;

namespace Samples.Wcf
{
    public class CustomErrorHandler : IErrorHandler
    {
        public bool HandleError(Exception error)
        {
            Console.WriteLine($"[Server] CustomErrorHandler.HandleError called with the following error: {error.Message}");

            // We expect to throw unhandled exceptions by the request processing pipeline
            // Return true so that the WCF session is not aborted
            return true;
        }

        public void ProvideFault(Exception error, MessageVersion version, ref Message fault)
        {
        }
    }
}
