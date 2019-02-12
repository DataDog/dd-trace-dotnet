using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web;
using System.Web.Configuration;
using Microsoft.AspNet.Identity;

namespace Samples.WebForms
{
    public class CoffeehouseApiClient
    {
        private static readonly string _apiBaseUrl = WebConfigurationManager.AppSettings.Get("CoffeehouseApiBaseUrl") ?? "http://localhost:8084";
        private static readonly HttpClient _apiClient = new HttpClient();

        private CoffeehouseApiClient() { }

        public static CoffeehouseApiClient Instance { get; } = new CoffeehouseApiClient();

        public IEnumerable<Product> GetProducts()
        {
            var url = _apiBaseUrl + "/products";

            var httpResponse = _apiClient.GetAsync(url).GetAwaiter().GetResult();

            return httpResponse.IsSuccessStatusCode
                       ? httpResponse.Content.ReadAsAsync<IEnumerable<Product>>().GetAwaiter().GetResult()
                       : Enumerable.Empty<Product>();
        }

        public Product GetProduct(string productId)
        {
            var url = _apiBaseUrl + "/products/" + productId;

            var httpResponse = _apiClient.GetAsync(url).GetAwaiter().GetResult();

            return httpResponse.IsSuccessStatusCode
                       ? httpResponse.Content.ReadAsAsync<Product>().GetAwaiter().GetResult()
                       : null;
        }

        public Order OrderSingleItem(string productId)
        {
            var userId = HttpContext.Current.User.Identity.GetUserId();

            var product = GetProduct(productId);

            if (product == null)
            {
                throw new ArgumentNullException(nameof(productId));
            }

            var orderUrl = _apiBaseUrl + "/orders";

            var order = new Order
                        {
                            Id = Guid.NewGuid().ToString("N"),
                            Status = "InProgress",
                            UserId = userId
                        };

            var orderResponse = _apiClient.PostAsJsonAsync(orderUrl, order)
                                          .GetAwaiter()
                                          .GetResult();

            orderResponse.EnsureSuccessStatusCode();

            var orderItemUrl = _apiBaseUrl + "/orderitems";

            var orderItem = new OrderItem
                            {
                                Id = Guid.NewGuid().ToString("N"),
                                OrderId = order.Id,
                                ProductId = productId,
                                Quantity = 1,
                                UnitCost = product.UnitCost > 0
                                               ? product.UnitCost + 0.01
                                               : 1.23
                            };

            var orderItemResponse = _apiClient.PostAsJsonAsync(orderItemUrl, orderItem)
                                              .GetAwaiter()
                                              .GetResult();

            orderItemResponse.EnsureSuccessStatusCode();

            return order;
        }
    }
}
