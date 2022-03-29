using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Samples.AspNetCoreRazorPages.Pages.Products
{
    public class ProductModel : PageModel
    {
        [BindProperty(SupportsGet = true)]
        [Range(minimum: 1, maximum: 1_000_000)] 
        public int Id { get; set; }

        public void OnGet()
        {
            if (!ModelState.IsValid)
            {
                Response.StatusCode = 400;
            }
        }
    }
}
