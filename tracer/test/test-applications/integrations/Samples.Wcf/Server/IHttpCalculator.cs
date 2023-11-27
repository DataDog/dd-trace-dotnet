using System.ServiceModel;
using System.ServiceModel.Web;
using System.Threading.Tasks;

namespace Samples.Wcf.Server;

[ServiceContract(Namespace = "WcfSample.Http")]
public interface IHttpCalculator
{
    [OperationContract]
    [WebGet(UriTemplate = "ServerSyncAddJson/{n1}/n2={n2}", RequestFormat = WebMessageFormat.Json, ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare)]
    double ServerSyncAddJson(string n1, string n2);

    [OperationContract]
    [WebGet(UriTemplate = "ServerSyncAddXml/{n1}/n2={n2}", RequestFormat = WebMessageFormat.Xml, ResponseFormat = WebMessageFormat.Xml, BodyStyle = WebMessageBodyStyle.Bare)]
    double ServerSyncAddXml(string n1, string n2);

    [OperationContract]
    [WebInvoke(UriTemplate = "ServerTaskAddPost", RequestFormat = WebMessageFormat.Json, ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare, Method = "POST")]
    Task<double> ServerTaskAddPost(CalculatorArguments arguments);

    [OperationContract]
    [WebInvoke(UriTemplate = "ServerSyncAddWrapped", RequestFormat = WebMessageFormat.Json, ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Wrapped)]
    double ServerSyncAddWrapped(string n1, string n2);
}
