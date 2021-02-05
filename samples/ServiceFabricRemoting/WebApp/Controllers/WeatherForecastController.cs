using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using WeatherService.Abstractions;

namespace WebApp.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly IWeatherService WeatherService = ServiceProxy.Create<IWeatherService>(new Uri("fabric:/ServiceFabricApplication/WeatherService"));

        private readonly ILogger<WeatherForecastController> _logger;

        public WeatherForecastController(ILogger<WeatherForecastController> logger)
        {
            _logger = logger;
        }

        [HttpGet("test")]
        public string Test()
        {
            return "Hello, world!";
        }

        [HttpGet]
        public async Task<WeatherForecast> Get()
        {
            return await WeatherService.GetWeather("Hello, world!");
        }
    }
}
