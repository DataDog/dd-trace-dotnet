using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using WeatherService.Abstractions;

namespace WebApp.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly IWeatherService WeatherServiceNetCore31 = ServiceProxy.Create<IWeatherService>(new Uri("fabric:/ServiceFabricApplication/WeatherService_NetCore31"));

        private static readonly IWeatherService WeatherServiceNetFx461 = ServiceProxy.Create<IWeatherService>(new Uri("fabric:/ServiceFabricApplication/WeatherService_NetFx461"));

        [HttpGet]
        public string Get()
        {
            return "Hello, world!";
        }

        [HttpGet("netcore31")]
        public async Task<WeatherForecast> GetNetCore31()
        {
            return await WeatherServiceNetCore31.GetWeather("Hello, world!");
        }

        [HttpGet("netfx461")]
        public async Task<WeatherForecast> GetNetFx461()
        {
            return await WeatherServiceNetFx461.GetWeather("Hello, world!");
        }
    }
}
