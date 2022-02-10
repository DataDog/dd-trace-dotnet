// <copyright file="Product.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace BuggyBits.Models
{
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
