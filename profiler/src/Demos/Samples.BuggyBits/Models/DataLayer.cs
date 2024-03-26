// <copyright file="DataLayer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace BuggyBits.Models
{
    public class DataLayer
    {
        private object syncobj = new object();
        private HttpClient _client = new HttpClient();

        public List<Product> GetFeaturedProducts()
        {
            lock (syncobj)
            {
                Thread.Sleep(5000);

                var featuredProducts = new List<Product>();
                featuredProducts.Add(new Product { ProductName = "Get out of meeting free card", Description = "May be kept until needed or sold", Price = "$500" });
                featuredProducts.Add(new Product { ProductName = "Solitaire cheat kit", Description = "Tired of not being able to finish your spider solitaire? this is the kit for you", Price = "$4999" });
                featuredProducts.Add(new Product { ProductName = "Bugspray", Description = "Why use a debugger when you can use bugspray?", Price = "$250.99" });
                featuredProducts.Add(new Product { ProductName = "In case of emergency push button", Description = "Excellent for any type of emergency", Price = "$1" });
                featuredProducts.Add(new Product { ProductName = "Vanity plate", Description = "Now available in 3 different colors and 5 different shapes", Price = "$33" });
                featuredProducts.Add(new Product { ProductName = "w00t baseball cap", Description = "Nothing will scare the other team as much as a w00t cap", Price = "$345" });
                featuredProducts.Add(new Product { ProductName = "Extra base", Description = "Are all your base belong to us? Purchase extra base upgrade", Price = "$22" });

                return featuredProducts;
            }
        }

        public ProductDetails GetProductInfo(string productName)
        {
            ProductDetails product = new ProductDetails();
            ShippingInfo shipping = new ShippingInfo();
            product.ProductName = productName;
            shipping.Distributor = "Buggy Bits";
            shipping.DaysToShip = 5;
            product.ShippingInfo = shipping;

            Type[] extraTypes = new Type[1];
            extraTypes[0] = typeof(ShippingInfo);

            MemoryStream stream = new MemoryStream();
            XmlSerializer serializer = new XmlSerializer(typeof(ProductDetails), extraTypes);
            serializer.Serialize(stream, product);

            // TODO: save off the data to an xml file or pass it as a string somewhere

            stream.Close();

            return product;
        }

        public List<Product> GetAllProducts()
        {
            var allProducts = new List<Product>();
            for (int i = 0; i < 10000; i++)
            {
                allProducts.Add(GetProduct(i));
            }

            return allProducts;
        }

        public void ApplyDiscount(Product product)
        {
            // apply a 25% discount if price is less than $200

            // Sub-optimal: use exception to check format and handle error
            try
            {
                var price = double.Parse(product.Price, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture);
                if (price < 200)
                {
                    product.Price = (price * 0.75).ToString();
                }
            }
            catch (System.FormatException)
            {
                throw new PriceException(product.Price, "Invalid price number");
            }
        }

        public List<Product> GetProductsOnSale()
        {
            var allProducts = new List<Product>(10000);
            for (int i = 0; i < 10000; i++)
            {
                var product = GetProduct(i);
                try
                {
                    ApplyDiscount(product);
                    allProducts.Add(product);
                }
                catch (PriceException)
                {
                    // TODO: log

                    continue;
                }
                catch (Exception e)
                {
                    Console.WriteLine("[Error] Ooops we did not catch this one " + e.GetType().ToString());
                }
            }

            return allProducts;
        }

        public bool TryApplyDiscount(Product product)
        {
            // apply a 25% discount if price is less than $200

            // Fix: use TryParse
            double price;
            if (double.TryParse(product.Price, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out price))
            {
                if (price < 200)
                {
                    product.Price = (price * 0.75).ToString();
                }

                return true;
            }

            return false;
        }

        public List<Product> GetProductsOnSaleEx()
        {
            var allProducts = new List<Product>(10000);
            for (int i = 0; i < 10000; i++)
            {
                var product = GetProduct(i);
                if (TryApplyDiscount(product))
                {
                    allProducts.Add(product);
                }
                else
                {
                    // TODO: log
                }
            }

            return allProducts;
        }

        // -------------------------------------------------------
        // Different asynchronous implementations
        public async Task<IEnumerable<Product>> GetAllProductAsync(string rootPath)
        {
            // Note: moving to asynchronous is much slower so reduce the number of product to return
            const int productCount = 2000;
            var path = GetProductInfoRoot(rootPath);
            var products = new List<Product>(productCount);
            for (int id = 0; id < productCount; id++)
            {
                products.Add(await GetProductAsync(path, id));
            }

            return products;
        }

        public IEnumerable<Task<Product>> GetAllProductTasks(string rootPath)
        {
            // Note: moving to asynchronous is much slower so reduce the number of product to return
            const int productCount = 2000;
            var path = GetProductInfoRoot(rootPath);
            var products = new List<Product>(productCount);
            for (int id = 0; id < productCount; id++)
            {
                yield return GetProductAsync(path, id);
            }
        }

        public IEnumerable<Product> GetAllProductsInParallel(string rootPath)
        {
            // Note: moving to asynchronous is much slower so reduce the number of product to return
            const int productCount = 2000;
            var path = GetProductInfoRoot(rootPath);
            var products = new List<Product>(productCount);

            // https://docs.microsoft.com/en-us/dotnet/standard/parallel-programming/how-to-write-a-parallel-for-loop-with-thread-local-variables
            Parallel.For(
                0,
                productCount,
                new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount * 4 },
                () => new List<Product>(),
                (id, _, perThreadList) => // download each product and store them in a lock-free per-thread list
                {
                    // "sync over async" anti-pattern
                    var product = GetProductAsync(path, id).GetAwaiter().GetResult();
                    perThreadList.Add(product);
                    return perThreadList;
                },
                (perThreadList) => // merge all per-thread list in a thread-safe way
                {
                    lock (products)
                    {
                        products.AddRange(perThreadList);
                    }
                });

            // note that products have to be sorted
            products.Sort((p1, p2) =>
            {
                var prefixLength = "product".Length;
                var sId1 = p1.ProductName.AsSpan(prefixLength, p1.ProductName.Length - prefixLength);
                var sId2 = p2.ProductName.AsSpan(prefixLength, p2.ProductName.Length - prefixLength);
                int.TryParse(sId1, out var id1);
                int.TryParse(sId2, out var id2);
                return id1 - id2;
            });
            return products;
        }

        public IEnumerable<Product> GetAllProductsInParallelWithLock(string rootPath)
        {
            // Note: moving to asynchronous is much slower so reduce the number of product to return
            const int productCount = 2000;
            var path = GetProductInfoRoot(rootPath);
            var products = new List<Product>(productCount);
            object listLock = new object();

            // https://docs.microsoft.com/en-us/dotnet/standard/parallel-programming/how-to-write-a-parallel-for-loop-with-thread-local-variables
            Parallel.For(
                0,
                productCount,
                new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 },
                (id) => // download each product and store them in a common list protected by a lock
                {
                    // don't create too many spans
                    var product = GetProduct(id);
                    lock (listLock)
                    {
                        products.Add(product);
                        Thread.Sleep(1);
                    }
                });

            // note that products have to be sorted
            products.Sort((p1, p2) =>
            {
                var prefixLength = "product".Length;
                var sId1 = p1.ProductName.AsSpan(prefixLength, p1.ProductName.Length - prefixLength);
                var sId2 = p2.ProductName.AsSpan(prefixLength, p2.ProductName.Length - prefixLength);
                int.TryParse(sId1, out var id1);
                int.TryParse(sId2, out var id2);
                return id1 - id2;
            });
            return products;
        }

        public async Task<Product> GetProductAsync(string path, int index)
        {
            var uri = $"{path}/{index}";
            try
            {
                var response = await _client.GetAsync(uri);
                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    return GetProduct(index);
                }

                var content = await response.Content.ReadAsStringAsync();
                var product = JsonSerializer.Deserialize<Product>(
                                content,
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return product;
            }
            catch (HttpRequestException)
            {
                // This might happen when the application exits
                // while incoming requests are still processed
                return GetProduct(index);
            }
        }

        public Product GetProduct(int index)
        {
            if (index % 2 == 0)
            {
                return new Product { ProductName = "Product" + index, Description = "Description for product" + index, Price = "119,99" };
            }
            else
            {
                return new Product { ProductName = "Product" + index, Description = "Description for product" + index, Price = "99" };
            }
        }

        public List<Link> GetAllLinks()
        {
            var links = new List<Link>()
            {
                new Link("If broken it is, fix it you should", "http://blogs.msdn.com/Tess"),
                new Link("Speaking of which...", "http://blogs.msdn.com/johan"),
                new Link("A developers stayings", "http://blogs.msdn.com/carloc"),
                new Link("Notes from a dark corner", "http://blogs.msdn.com/dougste"),
                new Link("Cheshire's blog", "http://blogs.msdn.com/jamesche"),
                new Link("ASP.NET debugging", "http://blogs.msdn.com/tom"),
                new Link("Nico's weblog", "http://blogs.msdn.com/nicd"),
                new Link("Todd Carter's weblog", "http://blogs.msdn.com/toddca")
            };
            return links;
        }

        private string GetProductInfoRoot(string rootPath)
        {
            return $"{rootPath}/Products/Info";
        }

        public class PriceException : Exception
        {
            public PriceException(string price, string message)
                : base(message)
            {
                Price = price;
            }

            public PriceException(string message)
                : base(message)
            {
            }

            public PriceException(string message, Exception inner)
                : base(message, inner)
            {
            }

            public string Price { get; private set; }
        }
    }
}
