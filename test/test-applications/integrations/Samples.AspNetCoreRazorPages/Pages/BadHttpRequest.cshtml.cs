using System;
using Datadog.Trace.Tests.DiagnosticListeners;
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
