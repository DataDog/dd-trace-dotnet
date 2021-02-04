using System;

namespace WeatherService.Abstractions
{
    public class WeatherForecast
    {
        public DateTime Date { get; set; }

        public int Temperature { get; set; }

        public string Message { get; set; }
    }
}
