// <copyright file="CompanyInformationController.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using BuggyBits.Models;
using Microsoft.AspNetCore.Mvc;

namespace BuggyBits.Controllers
{
    public class CompanyInformationController : Controller
    {
        private readonly DataLayer dataLayer;

        public CompanyInformationController(DataLayer dataLayer)
        {
            this.dataLayer = dataLayer;
        }

        public IActionResult Index(bool shortLived=false)
        {
            if (shortLived)
            {
                ViewData["TessGithubPage"] = GetGitHubPageViaThread();
                return View();
            }
            else
            {
                // bad blocking call
                ViewData["TessGithubPage"] = dataLayer.GetTessGithubPage().Result;
                return View();
            }
        }

        public IActionResult AccessGithub()
        {
            ViewData["TessGithubPage"] = $"Simulating a call to GitHub at {DateTime.Now.TimeOfDay}";
            return View("Index");
        }

        private string GetGitHubPageViaThread()
        {
            string result = string.Empty;
            Thread worker = new Thread(() =>
            {
                for (int i = 0; i < 5; i++)
                {
                    Thread.Sleep(100);
                }

                var rootPath = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host.Value}";
                result = dataLayer.SimulateGithubPage(rootPath).Result;
                for (int i = 0; i < 5; i++)
                {
                    Thread.Sleep(100);
                }
            });
            worker.Start();
            worker.Join();
            return result;
        }

        [HttpPost]
        public IActionResult Contact(ContactViewModel model)
        {
            BuggyMail mail = new BuggyMail();
            mail.SendEmail(model.Message, "whocares-at-buggymail");
            return View("Index");
        }
    }
}
