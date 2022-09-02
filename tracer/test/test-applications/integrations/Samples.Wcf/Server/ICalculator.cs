using System;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Samples.Wcf.Server
{
    /// <summary>
    ///     Note: after updating this interface you'll need to re-generate the proxy class <see cref="CalculatorClient"/>.
    ///     That can be done in a developer VS prompt (when the WCF server is running):
    ///         <code>svcutil.exe /language:cs /out:generatedProxy.cs /config:app.config http://localhost:8585/WcfSample/</code>
    ///     This makes a new file <c>generatedProxy.cs</c> and the contents of this can be swapped out with <see cref="CalculatorClient"/>.
    /// </summary>
    [ServiceContract(Namespace = "WcfSample")]
    public interface ICalculator
    {
        [OperationContract(Action = "")]
        double ServerEmptyActionAdd(double n1, double n2);

        [OperationContract]
        double ServerSyncAdd(double n1, double n2);

        [OperationContract]
        Task<double> ServerTaskAdd(double n1, double n2);

        [OperationContract(AsyncPattern = true)]
        IAsyncResult BeginServerAsyncAdd(double n1, double n2, AsyncCallback callback, object state);
        double EndServerAsyncAdd(IAsyncResult result);
    }
}
