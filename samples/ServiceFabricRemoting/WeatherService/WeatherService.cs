using System;
using System.Fabric;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Runtime;
using WeatherService.Abstractions;

namespace WeatherService
{
    /// <summary>
    /// An instance of this class is created for each service instance by the Service Fabric runtime.
    /// </summary>
    internal sealed class WeatherService : StatelessService, IWeatherService
    {
        public WeatherService(StatelessServiceContext context)
            : base(context)
        {
        }

        public Task<WeatherForecast> GetWeather(string message)
        {
            var rng = new Random();

            var forecast = new WeatherForecast
                           {
                               Date = DateTime.Now,
                               Temperature = rng.Next(-100, 100),
                               Message = message
                           };

            return Task.FromResult(forecast);
        }
    }
}
