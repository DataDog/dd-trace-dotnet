using System;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Samples.AspNetCoreRazorPages.Pages
{
    public class Error : PageModel
    {
        public void OnGet()
        {
            throw new Exception("Oops, an error!");
        }
    }
}
