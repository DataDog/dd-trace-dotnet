using System;
using Datadog.Trace.IntegrationTests.DiagnosticListeners;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Samples.AspNetCoreRazorPages.Pages
{
    public class BadHttpRequestModel : PageModel
    {
        public void OnGet()
        {
            ErrorHandlingHelper.ThrowBadHttpRequestException();
        }
    }
}
