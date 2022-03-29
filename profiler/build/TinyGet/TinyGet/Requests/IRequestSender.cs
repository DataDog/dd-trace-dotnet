using System.Threading.Tasks;

namespace TinyGet.Requests
{
    internal interface IRequestSender
    {
        Task Run();
    }
}