using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Samples.AspNetCoreRazorPages.Pages.Products
{
    public class ProductsIndexModel : PageModel
    {
        public void OnGet()
        {
        }

        public string Title { get; } = "Products";
    }
}
