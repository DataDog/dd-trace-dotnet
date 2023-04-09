// <copyright file="EndpointsCountController.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace BuggyBits.Controllers
{
    public class EndpointsCountController : Controller
    {
        [Route("End.Point.With.Dots")]
        public async Task<IActionResult> Index()
        {
            await Task.Delay(TimeSpan.FromSeconds(1));

            return View("Index");
        }
    }
}
