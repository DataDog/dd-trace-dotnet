// <copyright file="ProductsController.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BuggyBits.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BuggyBits.Controllers
{
    public class ProductsController : Controller
    {
        private readonly DataLayer dataLayer;

        public ProductsController(DataLayer dataLayer)
        {
            this.dataLayer = dataLayer;
        }

        // GET: Products asynchronously
        [Route("Products/Async")]
        public async Task<IActionResult> Async()
        {
            var sw = new Stopwatch();
            sw.Start();
            var products = await dataLayer.GetAllProductAsync($"{HttpContext.Request.Scheme}://{HttpContext.Request.Host.Value}");
            var productsTable = new StringBuilder(1024 * 100);
            productsTable.Append("<table><tr><th>Product Name</th><th>Description</th><th>Price</th></tr>");
            foreach (var product in products)
            {
                productsTable.Append($"<tr><td>{product.ProductName}</td><td>{product.Description}</td><td>${product.Price}</td></tr>");
            }

            productsTable.Append("</table>");
            sw.Stop();

            ViewData["ElapsedTimeInMs"] = sw.ElapsedMilliseconds;
            ViewData["ProductsTable"] = productsTable;
            return View("Index");
        }

        // GET: Products asynchronously with tasks
        [Route("Products/Task")]
        public async Task<IActionResult> Task()
        {
            var sw = new Stopwatch();
            sw.Start();
            var products = dataLayer.GetAllProductTasks($"{HttpContext.Request.Scheme}://{HttpContext.Request.Host.Value}");
            var productsTable = new StringBuilder(1024 * 100);
            productsTable.Append("<table><tr><th>Product Name</th><th>Description</th><th>Price</th></tr>");
            foreach (var getProductTask in products)
            {
                var product = await getProductTask;
                productsTable.Append($"<tr><td>{product.ProductName}</td><td>{product.Description}</td><td>${product.Price}</td></tr>");
            }

            productsTable.Append("</table>");
            sw.Stop();

            ViewData["ElapsedTimeInMs"] = sw.ElapsedMilliseconds;
            ViewData["ProductsTable"] = productsTable;
            return View("Index");
        }

        // GET: Products in parallel
        [Route("Products/Parallel")]
        public IActionResult Parallel()
        {
            var sw = new Stopwatch();
            sw.Start();
            var products = dataLayer.GetAllProductsInParallel($"{HttpContext.Request.Scheme}://{HttpContext.Request.Host.Value}");
            var productsTable = new StringBuilder(1024 * 100);
            productsTable.Append("<table><tr><th>Product Name</th><th>Description</th><th>Price</th></tr>");
            foreach (var product in products)
            {
                productsTable.Append($"<tr><td>{product.ProductName}</td><td>{product.Description}</td><td>${product.Price}</td></tr>");
            }

            productsTable.Append("</table>");
            sw.Stop();

            ViewData["ElapsedTimeInMs"] = sw.ElapsedMilliseconds;
            ViewData["ProductsTable"] = productsTable;
            return View("Index");
        }

        // GET: Products in parallel with a lock
        [Route("Products/ParallelLock")]
        public IActionResult ParallelLock()
        {
            var sw = new Stopwatch();
            sw.Start();
            var products = dataLayer.GetAllProductsInParallelWithLock($"{HttpContext.Request.Scheme}://{HttpContext.Request.Host.Value}");
            var productsTable = new StringBuilder(1024 * 100);
            productsTable.Append("<table><tr><th>Product Name</th><th>Description</th><th>Price</th></tr>");
            foreach (var product in products)
            {
                productsTable.Append($"<tr><td>{product.ProductName}</td><td>{product.Description}</td><td>${product.Price}</td></tr>");
            }

            productsTable.Append("</table>");
            sw.Stop();

            ViewData["ElapsedTimeInMs"] = sw.ElapsedMilliseconds;
            ViewData["ProductsTable"] = productsTable;
            return View("Index");
        }

        // GET: Product/Info/42
        [Route("Products/Info/{productId}")]
        [Produces("application/json")]
        public IActionResult Product(int productId)
        {
            var product = dataLayer.GetProduct(productId);
            return Json(product);
        }

        // GET: Products with less strings
        [Route("Products/Builder")]
        public IActionResult Builder()
        {
            var sw = new Stopwatch();
            sw.Start();
            var products = dataLayer.GetAllProducts();
            var productsTable = new StringBuilder(1000 * 80);  // try to avoid LOH allocations
            productsTable.Append("<table><tr><th>Product Name</th><th>Description</th><th>Price</th></tr>");
            foreach (var product in products)
            {
                productsTable.Append($"<tr><td>{product.ProductName}</td><td>{product.Description}</td><td>${product.Price}</td></tr>");
            }

            productsTable.Append("</table>");
            sw.Stop();

            ViewData["ElapsedTimeInMs"] = sw.ElapsedMilliseconds;
            ViewData["ProductsTable"] = productsTable;
            return View("Index");
        }

        // GET: Products
        public IActionResult Index()
        {
            var sw = new Stopwatch();
            sw.Start();
            var products = dataLayer.GetAllProducts();
            var productsTable = "<table><tr><th>Product Name</th><th>Description</th><th>Price</th></tr>";
            foreach (var product in products)
            {
                productsTable += $"<tr><td>{product.ProductName}</td><td>{product.Description}</td><td>${product.Price}</td></tr>";
            }

            productsTable += "</table>";
            sw.Stop();

            ViewData["ElapsedTimeInMs"] = sw.ElapsedMilliseconds;
            ViewData["ProductsTable"] = productsTable;
            return View();
        }

        // GET: Products/IndexSlow
        public IActionResult IndexSlow()
        {
            SpinWait.SpinUntil(() => false, TimeSpan.FromSeconds(1));

            var sw = new Stopwatch();
            sw.Start();
            var products = dataLayer.GetAllProducts();
            var productsTable = "<table><tr><th>Product Name</th><th>Description</th><th>Price</th></tr>";
            foreach (var product in products)
            {
                productsTable += $"<tr><td>{product.ProductName}</td><td>{product.Description}</td><td>${product.Price}</td></tr>";
            }

            productsTable += "</table>";
            sw.Stop();

            ViewData["ElapsedTimeInMs"] = sw.ElapsedMilliseconds;
            ViewData["ProductsTable"] = productsTable;

            return View("Index");
        }

        // GET: Products/Details/BugSpray
        [Route("Products/Details/{productName}")]
        public IActionResult Details(string productName)
        {
            var model = dataLayer.GetProductInfo(productName);
            return View(model);
        }

        // GET: Products/Featured
        public IActionResult Featured()
        {
            DateTime start = DateTime.Now;
            var model = dataLayer.GetFeaturedProducts();
            DateTime end = DateTime.Now;

            ViewData["StartTime"] = start.ToLongTimeString();
            ViewData["ExecutionTime"] = end.Subtract(start).Seconds + "." + end.Subtract(start).Milliseconds;

            return View(model);
        }

        // GET: Products on sale
        [Route("Products/Sales")]
        public IActionResult Sales()
        {
            var sw = new Stopwatch();
            sw.Start();

            // Exception-driven code
            var products = dataLayer.GetProductsOnSale();

            // Fix:
            // var products = dataLayer.GetProductsOnSaleEx();

            var productsTable = new StringBuilder(1000 * 80);  // try to avoid LOH allocations
            productsTable.Append("<table><tr><th>Product Name</th><th>Description</th><th>Price</th></tr>");
            foreach (var product in products)
            {
                productsTable.Append($"<tr><td>{product.ProductName}</td><td>{product.Description}</td><td>${product.Price}</td></tr>");
            }

            productsTable.Append("</table>");
            sw.Stop();

            ViewData["ElapsedTimeInMs"] = sw.ElapsedMilliseconds;
            ViewData["ProductsTable"] = productsTable;
            return View("Index");
        }
    }
}
