using System;
using Microsoft.AspNetCore.Mvc;

namespace Datadog.Trace.AspNetCore.Tests.Controllers
{
    public class TestController : Controller
    {
        public string Index(int id)
        {
            return "ActionContent";
        }

        public string Error()
        {
            throw new InvalidOperationException("Invalid");
        }
    }
}
