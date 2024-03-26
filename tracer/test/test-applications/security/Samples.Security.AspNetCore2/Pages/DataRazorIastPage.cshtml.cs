using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Samples.Security.AspNetCore5.Models;

namespace Samples.Security.AspNetCore2.Pages
{
    /// <summary>
    /// Cant use the one from Samples.AspNetCore5 because the attribute here works differently, needs the order not to be overriden
    /// The model is also not taken into account if it's a shortcut when building
    /// </summary>
    [IgnoreAntiforgeryToken(Order = 2001)]
    public class DataRazorIastPageModel : PageModel
    {
        public IActionResult OnGet()
        {
            return Content("Razor page ok");
        }

        [BindProperty]
        public MyModel MyModelInstance { get; set; }

        public IActionResult OnPost()
        {
            if (MyModelInstance.Property == "Execute")
            {
                try
                {
                    //Launch a command injection vulnerability
                    Process.Start(MyModelInstance.Property2);
                }
                catch
                {
                }
            }

            if (!ModelState.IsValid)
            {
                return Content("Incorrect model\n");
            }
            return Content("Correct model\n" + MyModelInstance.ToString());
        }
    }
}
