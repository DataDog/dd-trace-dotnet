using Microsoft.AspNetCore.Mvc;
using Samples.AspNetCore5.Models;

namespace Samples.AspNetCore5
{
    [IgnoreAntiforgeryToken]
    public class DataRazorPageModel : Microsoft.AspNetCore.Mvc.RazorPages.PageModel
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
                Content("correct model\n");
            }
            return Content("Incorrect model\n");
        }
    }
}
