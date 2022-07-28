// <copyright file="GrpcTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class GrpcLegacyTests : GrpcTestsBase
    {
        public GrpcLegacyTests(ITestOutputHelper output)
            : base("GrpcLegacy", output, usesAspNetCore: false)
        {
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.GrpcLegacy), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task SubmitTraces(string packageVersion)
        {
            GuardAlpine();
            GuardArm64(packageVersion);
            // Legacy doesn't use HttpClient at all
            await RunSubmitTraces(packageVersion, HttpClientIntegrationType.Disabled);
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void IntegrationDisabled()
        {
            GuardAlpine();

            var packageVersions = PackageVersions.GrpcLegacy
                                                 .Select(x => (string)x[0])
                                                 .Where(IsSupportedVersion)
                                                 .ToList();

            if (packageVersions.Count == 0)
            {
                throw new SkipException($"No supported package version available to run disabled tests");
            }

            GuardArm64(packageVersions[0]);
            RunIntegrationDisabled(packageVersions[0]);
        }
    }

#if NETCOREAPP3_0_OR_GREATER
    public class GrpcHttpTests : GrpcTestsBase
    {
        private static readonly Version MinimumSupportedNet5Version = new("2.32.0");

        public GrpcHttpTests(ITestOutputHelper output)
            : base("GrpcDotNet", output, usesAspNetCore: true)
        {
            SetEnvironmentVariable("ASPNETCORE_URLS", "http://127.0.0.1:0"); // don't use SSL
        }

        [SkippableTheory]
        [MemberData(nameof(GetData))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task SubmitTraces(string packageVersion, HttpClientIntegrationType httpClientType)
        {
            GuardAlpine();
            GuardArm64(packageVersion);

#if  NET5_0_OR_GREATER
            if (!string.IsNullOrEmpty(packageVersion) && new Version(packageVersion) < MinimumSupportedNet5Version)
            {
                throw new SkipException($"Can't run http tests on .NET 5+ with package version <{MinimumSupportedNet5Version}");
            }
#endif
            await RunSubmitTraces(packageVersion, httpClientType);
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void IntegrationDisabled()
        {
            GuardAlpine();

            var packageVersions = PackageVersions.Grpc
                                                .Select(x => (string)x[0])
#if NET5_0_OR_GREATER
                                                .Where(x => string.IsNullOrEmpty(x) || new Version(x) >= MinimumSupportedNet5Version)
#endif
                                                .Where(IsSupportedVersion)
                                                .ToList();

            if (packageVersions.Count == 0)
            {
                throw new SkipException($"No supported package version available to run http disabled tests");
            }

            GuardArm64(packageVersions[0]);

            RunIntegrationDisabled(packageVersions[0]);
        }
    }

    public class GrpcHttpsTests : GrpcTestsBase
    {
        public GrpcHttpsTests(ITestOutputHelper output)
            : base("GrpcDotNet", output, usesAspNetCore: true)
        {
            SetEnvironmentVariable("ASPNETCORE_URLS", "https://127.0.0.1:0"); // use SSL
        }

        [SkippableTheory]
        [MemberData(nameof(GetData))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task SubmitTraces(string packageVersion, HttpClientIntegrationType httpClientType)
        {
            GuardAlpine();
            GuardLinux();
            GuardArm64(packageVersion);

            await RunSubmitTraces(packageVersion, httpClientType);
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void IntegrationDisabled()
        {
            GuardAlpine();
            GuardLinux();

            var packageVersions = PackageVersions.Grpc
                                                .Select(x => (string)x[0])
                                                .Where(IsSupportedVersion)
                                                .ToList();

            if (packageVersions.Count == 0)
            {
                throw new SkipException($"No supported package version available to run http disabled tests");
            }

            GuardArm64(packageVersions[0]);

            RunIntegrationDisabled(packageVersions[0]);
        }

        private static void GuardLinux()
        {
            if (RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
            {
                throw new SkipException("Can't run https tests on Linux");
            }
        }
    }
#endif

    [UsesVerify]
    public abstract class GrpcTestsBase : TestHelper
    {
        private const string MetadataHeaders = "server-value1,server-value2:servermeta,client-value1,client-value2:clientmeta";
        private static readonly Regex GrpcCoreCreatedRegex = new(@"\@\d{10}\.\d{9}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex GrpcCoreFileLineRegex = new(@"""file_line""\:\d+,", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private readonly bool _usesAspNetCore;

        protected GrpcTestsBase(string sampleName, ITestOutputHelper output, bool usesAspNetCore)
            : base(sampleName, output)
        {
            _usesAspNetCore = usesAspNetCore;
            SetEnvironmentVariable(ConfigurationKeys.GrpcTags, MetadataHeaders);
        }

        /// <summary>
        /// Settings for testing the Grpc/HttpClient interaction
        /// </summary>
        public enum HttpClientIntegrationType
        {
            /// <summary>
            /// Use the default instrumentation (disable socket instrumentation)
            /// </summary>
            Default,

            /// <summary>
            /// Use the socket handler instrumentation
            /// </summary>
            SocketHandler,

            /// <summary>
            /// Disable HttpClient instrumentation
            /// </summary>
            Disabled
        }

        public static IEnumerable<object[]> GetData()
            => from packageVersionArray in PackageVersions.Grpc
               from httpClientType in Enum.GetValues(typeof(HttpClientIntegrationType)).Cast<HttpClientIntegrationType>()
               select new[] { packageVersionArray[0], httpClientType };

        protected async Task RunSubmitTraces(
            string packageVersion,
            HttpClientIntegrationType httpClientIntegrationType)
        {
            const int requestCount = 2 // Unary  (sync + async)
                                    + 1 // 1 server streaming
                                    + 1 // 1 client streaming
                                    + 1 // 1 both streaming
                                    + 2 // Deadline exceeded (sync + async)
                                    + (4 * 2); // 4 Error types (sync + async)

            // Get between 3 and 5 spans per request:
            // (grpc + http) outbound + (grpc + aspnetcore) inbound
            var isGrpcSupported = IsSupportedVersion(packageVersion);
            var httpInstrumentationEnabled = httpClientIntegrationType != HttpClientIntegrationType.Disabled;

            var spansPerRequest = isGrpcSupported ? 3 : 1; // minimum
            if (_usesAspNetCore)
            {
                spansPerRequest++;
            }

            if (httpInstrumentationEnabled)
            {
                spansPerRequest++;
            }

            switch (httpClientIntegrationType)
            {
                case HttpClientIntegrationType.Default:
                    SetEnvironmentVariable("DOTNET_SYSTEM_NET_HTTP_USESOCKETSHTTPHANDLER", "0");
                    break;

                case HttpClientIntegrationType.SocketHandler:
                    SetEnvironmentVariable("DOTNET_SYSTEM_NET_HTTP_USESOCKETSHTTPHANDLER", "1");
                    break;

                case HttpClientIntegrationType.Disabled:
                    SetEnvironmentVariable("DD_DISABLED_INTEGRATIONS", "HttpMessageHandler;HttpSocketsHandler;WinHttpHandler");
                    break;
                default:
                    throw new InvalidOperationException("Unknown HttpClientIntegrationType: " + httpClientIntegrationType);
            }

            var totalExpectedSpans = (requestCount * spansPerRequest);

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var assert = new AssertionScope();
            using (var processResult = RunSampleAndWaitForExit(agent, packageVersion: packageVersion, aspNetCorePort: 0))
            {
                var spans = agent.WaitForSpans(totalExpectedSpans, 500);

                // There is a race condition in GRPC version < v2.43.0 that can cause ObjectDisposedException
                // when a deadline is exceeded. Skip the test if we hit it: https://github.com/grpc/grpc-dotnet/pull/1550
                if (processResult.ExitCode != 0
                    && (string.IsNullOrEmpty(packageVersion) || new Version(packageVersion) < new Version("2.43.0")))
                {
                    if (processResult.StandardError.Contains("ObjectDisposedException"))
                    {
                        throw new SkipException("Hit race condition in GRPC deadline exceeded");
                    }
                }

                using var scope = new AssertionScope();
                spans.Count.Should().Be(totalExpectedSpans);

                if (!isGrpcSupported)
                {
                    Output.WriteLine($"Package version {packageVersion} is not supported in Grpc, skipping snapshot verification");
                    telemetry.AssertIntegrationDisabled(IntegrationId.Grpc);
                    return;
                }

                var settings = VerifyHelper.GetSpanVerifierSettings();
                // Grpc.Core creates nasty exception messages that we need to scrub so they're consistent
                settings.AddRegexScrubber(GrpcCoreCreatedRegex, "@00000000000.000000000");
                // Different versions of Grpc.Core will have a different file line
                settings.AddRegexScrubber(GrpcCoreFileLineRegex, @"""file_line"":1234,");
                // Depending on the exact code paths taken, the error status may be either of these:
                settings.AddSimpleScrubber("DeadlineExceeded", "Deadline Exceeded");
                // Keep the traces the same between http and https endpoints
                settings.AddSimpleScrubber("https://", "http://");
                // Linux vs Windows have different file paths in stack traces (legacy grpc)
                settings.AddSimpleScrubber(@"T:\src\github\grpc\workspace_csharp_ext_windows_x64\", @"..\..\..\");
                settings.AddSimpleScrubber(@"T:\src\github\grpc\workspace_csharp_ext_windows_x86\", @"..\..\..\");
                settings.AddSimpleScrubber("/var/local/git/grpc/src/core/lib/surface/", @"..\..\..\src\core\lib\surface\");
                // net461 has a different call stack
                settings.AddSimpleScrubber(
                    "at Grpc.Core.Internal.AsyncCallServer`2.SendStatusFromServerAsync(Status status, Metadata trailers, Nullable`1 optionalWrite)",
                    "at Grpc.Core.Utils.GrpcPreconditions.CheckState(Boolean condition)");

                // There are inconsistencies in the very slow spans depending on the exact order that things get cancelled
                // so normalise them all
                FixVerySlowServerSpans(spans, httpClientIntegrationType);

                if (EnvironmentHelper.SampleName == "GrpcLegacy")
                {
                    FixVerySlowClientSpans(spans);
                }

                var grpcSpans = spans.Where(s => s.Name == "grpc.request")
                                     .ToList();

                foreach (var grpcSpan in grpcSpans)
                {
                    var result = grpcSpan.IsGrpc();
                    Assert.True(result.Success, result.ToString());
                }

                await VerifyHelper.VerifySpans(spans, settings)
                                  .UseTypeName(EnvironmentHelper.SampleName)
                                  .UseTextForParameters($"httpclient={httpInstrumentationEnabled}")
                                  .DisableRequireUniquePrefix();

                static void FixVerySlowServerSpans(IImmutableList<MockSpan> spans, HttpClientIntegrationType httpClientIntegrationType)
                {
                    // normalise the grpc.request very slow spans
                    // These _may_ not get the expected values (though the _client_ spans always will)
                    // Depending on how the server handles them
                    var verySlowGrpcServerSpans = spans
                                                 .Where(x => x.Name == "grpc.request" && x.Resource.EndsWith("VerySlow") && x.Tags["span.kind"] == "server")
                                                 .ToList();
                    foreach (var span in verySlowGrpcServerSpans)
                    {
                        span.Error = 1;
                        span.Tags["error.msg"] = "Deadline Exceeded";
                        span.Tags.Remove("error.stack");
                        span.Tags.Remove("error.type");
                        span.Tags["grpc.status.code"] = "4";
                    }

                    var verySlowAspNetCoreServerSpans = spans
                                                 .Where(x => x.Name == "aspnet_core.request" && x.Resource.EndsWith("veryslow"))
                                                 .ToList();

                    foreach (var span in verySlowAspNetCoreServerSpans)
                    {
                        // May be missing in some cases
                        span.Tags["http.status_code"] = "200";
                    }

                    // there is a race between the server cancelling a deadline and the client cancelling it
                    // which can lead to inconsistency in the httpclient span, so "fix" the cancelled http client spans
                    if (httpClientIntegrationType != HttpClientIntegrationType.Disabled)
                    {
                        var httpClientSpans = spans
                                             .Where(x => x.Name == "http.request" && x.Resource.EndsWith("VerySlow"))
                                             .ToList();
                        httpClientSpans.Should().HaveCount(2);

                        foreach (var span in httpClientSpans)
                        {
                            span.Error = 0;
                            span.Tags.Remove("error.msg");
                            span.Tags.Remove("error.type");
                            span.Tags.Remove("error.stack");
                            span.Tags["http.status_code"] = "200";
                        }
                    }
                }

                static void FixVerySlowClientSpans(IImmutableList<MockSpan> spans)
                {
                    var verySlowGrpcClientSpans = spans
                                                 .Where(x => x.Name == "grpc.request" && x.Resource.EndsWith("VerySlow") && x.Tags["span.kind"] == "client")
                                                 .ToList();

                    // Grpc.Core 2.45.0 started using very different paths and messages in the
                    // deadline paths. For simplicity, normalise these to something simple
                    foreach (var span in verySlowGrpcClientSpans)
                    {
                        span.Tags["error.msg"] = "Deadline Exceeded";
                        span.Tags.Remove("error.stack");
                    }
                }
            }

            telemetry.AssertIntegrationEnabled(IntegrationId.Grpc);
        }

        protected void RunIntegrationDisabled(string packageVersion)
        {
            using var telemetry = this.ConfigureTelemetry();
            SetEnvironmentVariable($"DD_TRACE_{nameof(IntegrationId.Grpc)}_ENABLED", "false");
            using var agent = EnvironmentHelper.GetMockAgent();
            using var process = RunSampleAndWaitForExit(agent, packageVersion: packageVersion, aspNetCorePort: 0);
            var spans = agent.WaitForSpans(1, timeoutInMilliseconds: 500).Where(s => s.Type == "grpc.request").ToList();

            Assert.Empty(spans);
            telemetry.AssertIntegrationDisabled(IntegrationId.Grpc);
        }

        protected void GuardAlpine()
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IsAlpine")))
            {
                throw new SkipException("GRPC.Tools does not support Alpine");
            }
        }

        protected void GuardArm64(string packageVersion)
        {
            var minVersion = new Version("2.37.0");

            if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                && (string.IsNullOrEmpty(packageVersion) || new Version(packageVersion) < minVersion))
            {
                throw new SkipException($"GRPC.Tools does not support ARM64 on version < {minVersion}");
            }
        }

        protected bool IsSupportedVersion(string packageVersion)
        {
            return string.IsNullOrEmpty(packageVersion)
                || new Version(packageVersion) >= new Version("2.30.0");
        }
    }
}
