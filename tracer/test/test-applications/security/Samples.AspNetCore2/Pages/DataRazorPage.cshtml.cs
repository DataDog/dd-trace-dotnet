using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Samples.AspNetCore5.Models;

namespace Samples.AspNetCore2.Pages
{
    /// <summary>
    /// Cant use the one from Samples.AspNetCore5 because the attribute here works differently, needs the order not to be overriden
    /// The model is also not taken into account if it's a shortcut when building
    /// </summary>
    [IgnoreAntiforgeryToken(Order = 2000)]
    public class DataRazorPageModel : PageModel
    {
        public IActionResult OnGet()
        {
            return Content("Razor page ok");
        }

        [BindProperty]
        public MyModel MyModel { get; set; }

        public IActionResult OnPost()
        {
            if (!ModelState.IsValid)
            {
                return Content("Inccorrect model\n");
            }
            return Content("Correct model\n");
        }
    }
}
