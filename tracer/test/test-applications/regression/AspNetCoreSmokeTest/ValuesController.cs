using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AspNetCoreSmokeTest
{
    [ApiController]
    public class ValuesController : ControllerBase
    {
        private readonly ILogger<ValuesController> _logger;

        public ValuesController(ILogger<ValuesController> logger)
        {
            _logger = logger;
        }

        [HttpGet("/api/values")]
        public string Get()
        {
            _logger.LogInformation("Received request");
            return Program.GetTracerAssemblyLocation();
        }
    }
}
