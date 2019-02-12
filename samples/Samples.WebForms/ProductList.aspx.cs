using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.ModelBinding;
using System.Web.UI;

namespace Samples.WebForms
{
    public partial class ProductList : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
        }

        public IEnumerable<Product> GetProducts([QueryString("id")] string productId, [RouteData] string categoryName)
        {
            if (!string.IsNullOrEmpty(productId))
            {
                var product = CoffeehouseApiClient.Instance.GetProduct(productId);

                return new[] { product };
            }

            var products = CoffeehouseApiClient.Instance.GetProducts();

            return string.IsNullOrEmpty(categoryName)
                       ? products.Take(count: 50)
                       : products.Where(p => p.ProductType == categoryName).Take(count: 50);
        }

        public bool IsAuthenticated()
            => HttpContext.Current?.User != null && HttpContext.Current.User.Identity.IsAuthenticated;
    }
}
