// <copyright file="ReviewsController.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using BuggyBits.Models;
using Microsoft.AspNetCore.Mvc;

namespace BuggyBits.Controllers
{
    public class ReviewsController : Controller
    {
        // GET: Reviews/1
        [Route("Reviews/{refresh?}")]
        public IActionResult Index(int? refresh)
        {
            if (refresh != null)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            var review1 = new Review(1);
            ViewData["Review1_Quote"] = review1.Quote;
            ViewData["Review1_Source"] = review1.Source;
            review1.Clear();

            var review2 = new Review(2);
            ViewData["Review2_Quote"] = review2.Quote;
            ViewData["Review2_Source"] = review2.Source;
            review2.Clear();

            return View();
        }
    }
}
