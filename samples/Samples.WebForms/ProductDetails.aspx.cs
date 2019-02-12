using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.ModelBinding;
using System.Web.UI;

namespace Samples.WebForms {
    public partial class ProductDetails : Page
    {
        protected void Page_Load(object sender, EventArgs e) { }

        public IEnumerable<Product> GetProduct([QueryString("id")] string productId, [RouteData] string productName)
        {
            if (!string.IsNullOrEmpty(productId))
            {
                var product = CoffeehouseApiClient.Instance.GetProduct(productId);

                return new[] { product };
            }

            var products = CoffeehouseApiClient.Instance.GetProducts();

            return string.IsNullOrEmpty(productName)
                       ? products.Take(count: 50)
                       : products.Where(p => p.Name == productName).Take(count: 50);
        }
    }
}
