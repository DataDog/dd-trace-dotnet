using Microsoft.AspNetCore.Mvc;
using Samples.Security.AspNetCore5.Models;

namespace Samples.Security.AspNetCore5
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
                return Content("Inccorrect model\n");
            }
            return Content("Correct model\n" + MyModel.ToString());
        }
    }
}
