using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MismatchedTracerVersions.AspNetCore.Controllers
{
    [ApiController]
    [Route("/")]
    public class TestController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<TestController> _logger;

        public TestController(IConfiguration configuration, ILogger<TestController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ApiModel> Get()
        {
            string url = _configuration[WebHostDefaults.ServerUrlsKey];

            // call GetTimestamp() through an http call, to simulate a distributed trace
            HttpClient httpClient = new();
            string timestamp = await httpClient.GetStringAsync($"{url}/timestamp");

            return new ApiModel
            {
                Timestamp = timestamp,
                Assemblies = GetAssemblies()
            };
        }

        [HttpGet("assemblies")]
        public IEnumerable<string> GetAssemblies()
        {
            var appDomainAssemblies = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.FullName);
            var loadContextAssemblies = System.Runtime.Loader.AssemblyLoadContext.All.SelectMany(alc => alc.Assemblies).Select(a => a.FullName);

            return appDomainAssemblies.Concat(loadContextAssemblies)
                                      .Where(a => a.StartsWith("Datadog"))
                                      .Distinct()
                                      .OrderBy(a => a);
        }

        [HttpGet("timestamp")]
        public string GetTimestamp()
        {
            return DateTime.UtcNow.ToString();
        }
    }
}
