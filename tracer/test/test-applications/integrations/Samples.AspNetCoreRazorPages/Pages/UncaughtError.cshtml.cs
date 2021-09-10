using System;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Samples.AspNetCoreRazorPages.Pages
{
    public class UncaughtErrorModel : PageModel
    {
        public void OnGet()
        {
            throw new InvalidOperationException();
        }
    }
}
