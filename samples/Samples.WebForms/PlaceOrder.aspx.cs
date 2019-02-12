using System;
using System.Collections.Generic;
using System.Web.ModelBinding;
using System.Web.UI;

namespace Samples.WebForms {
    public partial class PlaceOrder : Page
    {
        protected void Page_Load(object sender, EventArgs e) { }

        public IEnumerable<Order> PlaceSingleItemOrder([QueryString("productId")] string productId)
        {
            var order = CoffeehouseApiClient.Instance.OrderSingleItem(productId);

            return new[] { order };
        }
    }
}
