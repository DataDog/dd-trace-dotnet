using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace Samples.Transport.UnixDomainSocket.Pages
{
    public class MockAgentModel : PageModel
	{
		private readonly ILogger<MockAgentModel> _logger;

		public MockAgentModel(ILogger<MockAgentModel> logger)
		{
			_logger = logger;
		}

		public void OnGet()
        {
            using (Datadog.Trace.Tracer.Instance.StartActive("on-get"))
            {
                _logger.LogInformation("We got the agent get");
            }
        }
	}
}
