using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace Samples.Transport.UnixDomainSocket.Pages
{
    public class IndexModel : PageModel
	{
		private readonly ILogger<IndexModel> _logger;

		public IndexModel(ILogger<IndexModel> logger)
		{
			_logger = logger;
		}

		public void OnGet()
        {
            using (Datadog.Trace.Tracer.Instance.StartActive("on-get"))
            {
                _logger.LogInformation("We got the get");
            }
        }
	}
}
