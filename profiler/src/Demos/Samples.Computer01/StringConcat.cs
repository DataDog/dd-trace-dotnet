// <copyright file="StringConcat.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Samples.Computer01
{
    public class StringConcat : ScenarioBase
    {
        private int _count;

        public StringConcat(int count = 10000)
        {
            _count = count;
        }

        public override void OnProcess()
        {
            AllocateStrings();
        }

        // from BuggyBits
        public void AllocateStrings()
        {
            var sw = new Stopwatch();
            sw.Start();

            var products = GetAllProducts(_count);
            var productsTable = "<table><tr><th>Product Name</th><th>Description</th><th>Price</th></tr>";
            foreach (var product in products)
            {
                productsTable += $"<tr><td>{product.ProductName}</td><td>{product.Description}</td><td>${product.Price}</td></tr>";
            }

            productsTable += "</table>";
            sw.Stop();
            Console.WriteLine($"{products.Count} products for {productsTable.Length} characters in {sw.ElapsedMilliseconds} ms");
        }

        public List<Product> GetAllProducts(int count)
        {
            var allProducts = new List<Product>();
            for (int i = 0; i < count; i++)
            {
                allProducts.Add(GetProduct(i));
            }

            return allProducts;
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

        public class Product
        {
            public string ProductName { get; set; }
            public string Description { get; set; }
            public string Price { get; set; }

            // useful when debugging a List<Product>
            public override string ToString()
            {
                return ProductName;
            }
        }
    }
}
