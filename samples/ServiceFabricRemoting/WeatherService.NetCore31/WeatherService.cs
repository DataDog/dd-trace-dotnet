using System;
using System.Collections.Generic;
using System.Fabric;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using WeatherService.Abstractions;

namespace WeatherService.NetCore31
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

        /// <summary>
        /// Optional override to create listeners (e.g., TCP, HTTP) for this service replica to handle client or user requests.
        /// </summary>
        /// <returns>A collection of listeners.</returns>
        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return this.CreateServiceRemotingInstanceListeners();
        }

        public Task<WeatherForecast> GetWeather(string message)
        {
            var rng = new Random();

            var forecast = new WeatherForecast
                           {
                               Date = DateTime.Now,
                               Temperature = rng.Next(-100, 100),
                               Message = message,
                               Service = typeof(WeatherService).FullName
                           };

            return Task.FromResult(forecast);
        }
    }
}
