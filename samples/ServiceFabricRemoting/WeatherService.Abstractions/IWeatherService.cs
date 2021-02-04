using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Remoting;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport;

[assembly: FabricTransportServiceRemotingProvider(RemotingListenerVersion = RemotingListenerVersion.V2_1, RemotingClientVersion = RemotingClientVersion.V2_1)]

namespace WeatherService.Abstractions
{
    public interface IWeatherService : IService
    {
        Task<WeatherForecast> GetWeather(string message);
    }
}
