using System;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Samples.Wcf.Server
{
    [ServiceContract(Namespace = "WcfSample")]
    public interface ICalculator
    {
        [OperationContract]
        double ServerSyncAdd(double n1, double n2);

        [OperationContract]
        Task<double> ServerTaskAdd(double n1, double n2);

        [OperationContract(AsyncPattern = true)]
        IAsyncResult BeginServerAsyncAdd(double n1, double n2, AsyncCallback callback, object state);
        double EndServerAsyncAdd(IAsyncResult result);
    }
}
