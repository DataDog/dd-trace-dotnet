// <copyright file="WebsiteAspNetCore01Test.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using Xunit.Abstractions;

namespace Datadog.Profiler.SmokeTests
{
    public partial class WebsiteAspNetCore01Test
    {
        private readonly ITestOutputHelper _output;

        public WebsiteAspNetCore01Test(ITestOutputHelper output)
        {
            _output = output;
        }

        [SmokeFact("Datadog.Demos.Website-AspNetCore01", DisplayName = "Website-AspNetCore01")]
        public void CheckSmoke(string appName, string framework, string appAssembly)
        {
            new SmokeTestRunner(appName, framework, appAssembly, _output).RunAndCheck();
        }
    }
}
