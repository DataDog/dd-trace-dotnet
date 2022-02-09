// <copyright file="Index.cshtml.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

#pragma warning disable SA1649 // File name should match first type name
namespace Datadog.Demos.Website_AspNetCore01.Pages
{
    public class IndexModel : PageModel
    {
        public const int IterationCount = 100000;
        public const int UselessArrayLength = 10000;

        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ILogger<IndexModel> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
        }

        public string GetPiComputationResults()
        {
            Guid guid = Guid.NewGuid();

            double[] uselessArray = new double[UselessArrayLength];
            for (int i = 0; i < UselessArrayLength; i++)
            {
                double pi = ComputePi();
                uselessArray[i] = pi;
            }

            return $"Pi: {uselessArray[0]}; Number of times stored in memory: {UselessArrayLength}; Uniqueness id: {guid.ToString("D")}.";
        }

        private double ComputePi()
        {
            ulong denominator = 1;
            int numerator = 1;
            double pi = 1;

            for (int i = 0; i < IterationCount; i++)
            {
                numerator = -numerator;
                denominator += 2;
                pi += ((double)numerator) / ((double)denominator);
            }

            pi *= 4.0;

            return pi;
        }
    }
}
#pragma warning restore SA1649