// <copyright file="AspNetCoreIisMvcTestBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NETCOREAPP

using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using NUnit.Framework;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    public abstract class AspNetCoreIisMvcTestBase : IisTestsBase
    {
        private readonly bool _inProcess;
        private readonly bool _enableRouteTemplateResourceNames;

        protected AspNetCoreIisMvcTestBase(string sampleName, bool inProcess, bool enableRouteTemplateResourceNames)
            : base(sampleName, inProcess ? IisAppType.AspNetCoreInProcess : IisAppType.AspNetCoreOutOfProcess)
        {
            _inProcess = inProcess;
            _enableRouteTemplateResourceNames = enableRouteTemplateResourceNames;
            SetEnvironmentVariable(ConfigurationKeys.HttpServerErrorStatusCodes, "400-403, 500-503");

            SetServiceVersion("1.0.0");

            SetCallTargetSettings(true);
            if (enableRouteTemplateResourceNames)
            {
                SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, "true");
            }
        }

        public static IEnumerable<TestCaseData> Data() => new TestCaseData[]
        {
            new("/", 200),
            new("/delay/0", 200),
            new("/api/delay/0", 200),
            new("/not-found", 404),
            new("/status-code/203", 203),
            new("/status-code/500", 500),
            new("/bad-request", 500),
            new("/status-code/402", 402),
            new("/ping", 200),
            new("/branch/ping", 200),
            new("/branch/not-found", 404),
        };

        protected string GetTestName(string testName)
        {
            return testName
                 + (_inProcess ? ".InProcess" : ".OutOfProcess")
                 + (_enableRouteTemplateResourceNames ? ".WithFF" : ".NoFF");
        }
    }
}
#endif
