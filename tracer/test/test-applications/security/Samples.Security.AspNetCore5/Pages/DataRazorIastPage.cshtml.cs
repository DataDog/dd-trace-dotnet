using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Samples.Security.AspNetCore5.Models;

namespace Samples.Security.AspNetCore5
{
    [IgnoreAntiforgeryToken]
    public class DataRazorIastPageModel : Microsoft.AspNetCore.Mvc.RazorPages.PageModel
    {
        public IActionResult OnGet()
        {
            return Content("Razor page ok");
        }

        [BindProperty]
        public MyModel MyModel { get; set; }

        public IActionResult OnPost()
        {
            if (MyModel.Property == "Execute")
            {
                try
                {
                    //Launch a command injection vulnerability
                    Process.Start(MyModel.Property2);
                }
                catch
                {
                }
            }

            if (!ModelState.IsValid)
            {
                return Content("Incorrect model\n");
            }
            return Content("Correct model\n" + MyModel.ToString());
        }
    }
}
