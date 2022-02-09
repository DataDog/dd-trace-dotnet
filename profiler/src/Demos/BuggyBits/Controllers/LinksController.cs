// <copyright file="LinksController.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using BuggyBits.Models;
using Microsoft.AspNetCore.Mvc;

namespace BuggyBits.Controllers
{
    public class LinksController : Controller
    {
        private readonly DataLayer dataLayer;
        public LinksController(DataLayer dataLayer)
        {
            this.dataLayer = dataLayer;
        }

        public IActionResult Index()
        {
            return View(dataLayer.GetAllLinks());
        }
    }
}
