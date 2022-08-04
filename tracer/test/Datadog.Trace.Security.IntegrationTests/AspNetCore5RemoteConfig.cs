// <copyright file="AspNetCore5RemoteConfig.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    public class AspNetCore5RemoteConfig : AspNetCoreBase, IDisposable
    {
        public AspNetCore5RemoteConfig(ITestOutputHelper outputHelper)
            : base("AspNetCore5", outputHelper, "/shutdown")
        {
        }

        [SkippableTheory]
        [InlineData(AddressesConstants.RequestPathParams, HttpStatusCode.OK, "/params-endpoint/appscan_fingerprint")]
        [Trait("RunOnWindows", "True")]
        public async Task TestActivation(string test, HttpStatusCode expectedStatusCode, string url = DefaultAttackUrl)
        {
            // setup RCM
            var sampleAppPath = EnvironmentHelper.GetSampleApplicationOutputDirectory();
            var remoteConfigDir = Path.Combine(sampleAppPath, "RemoteConfig");

            Directory.CreateDirectory(remoteConfigDir);
            Console.WriteLine("Created directory remoteConfigDir: " + remoteConfigDir);

            // normal setup
            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            var settings = VerifyHelper.GetSpanVerifierSettings(test, (int)expectedStatusCode, sanitisedUrl);

            var agent = await RunOnSelfHosted(false);

            // first test - no security
            var preSpans = await TestAppSecRequestAsync(agent, url, null, 5, 1, settings);

            // activate security via remote config
            var assembly = typeof(AspNetCore5RemoteConfig).Assembly;
            var fileStream = assembly.GetManifestResourceStream("Datadog.Trace.Security.IntegrationTests.FEATURES.json");

            using (var outFile = File.OpenWrite(Path.Combine(remoteConfigDir, "FEATURES.json")))
            {
                fileStream.CopyTo(outFile);
            }

            // this definitely will not cause flake or slow the CI
            await Task.Delay(TimeSpan.FromSeconds(5));

            // second test - security enabled
            var postSpans = await TestAppSecRequestAsync(agent, url, null, 5, 1, settings);

            var allResults = new List<MockSpan>();
            allResults.AddRange(preSpans);
            allResults.AddRange(postSpans);

            await Verifier.Verify(allResults, settings)
              .UseMethodName("_")
              .UseTypeName(GetTestName());
        }

        protected override string GetTestName()
        {
            return "Security." + nameof(AspNetCore5RemoteConfig);
        }
    }
}
#endif
