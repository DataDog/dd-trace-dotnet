// <copyright file="AwsLambdaSdkTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;
using static Datadog.Trace.TestHelpers.SpanMetadataRulesHelpers;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AWS;

[UsesVerify]
public class AwsLambdaSdkTests : TracingIntegrationTest
{
    public AwsLambdaSdkTests(ITestOutputHelper output)
        : base("AWS.Lambda.Invoke", output)
    {
        // Keep the sample's local HTTP endpoint out of the trace assertions.
        SetEnvironmentVariable("DD_DISABLED_INTEGRATIONS", "HttpMessageHandler,HttpSocketsHandler,WebRequest");
    }

    public static IEnumerable<object[]> GetEnabledConfig()
        => from version in PackageVersions.AwsLambdaSdk
           from schema in new[] { "v0", "v1" }
           select new object[] { version[0], schema };

    public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => Result.FromSpan(span)
        .Properties(s => s.Matches(Name, metadataSchemaVersion == "v0" ? "lambda.request" : "aws.lambda.invoke").Matches(Type, "http"))
        .Tags(s => s
                 .Matches("component", "aws-sdk")
                 .Matches("span.kind", "client")
                 .Matches("aws.agent", "dotnet-aws-sdk")
                 .Matches("aws.operation", "Invoke")
                 .Matches("aws.service", "Lambda")
                 .Matches("aws_service", "Lambda")
                 .IsPresent("functionname")
                 .IsPresent("aws.lambda")
                 .IsPresent("http.method")
                 .IsPresent("http.url")
                 .IsOptional("http.status_code")
                 .IsOptional("aws.requestId")
                 .IsOptional("aws.region")
                 .IsOptional("region")
                 .IsOptional("_dd.base_service")
                 .IsOptional("_dd.tags.process")
                 .IsOptional("_dd.svc_src"));

    [SkippableTheory]
    [MemberData(nameof(GetEnabledConfig))]
    [Trait("Category", "EndToEnd")]
    public async Task PropagatesRequestResponseContext(string packageVersion, string schema)
    {
        SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", schema);
        // Disabling the handler integration must not disable AWS SDK calls from an ordinary process.
        SetEnvironmentVariable("DD_TRACE_AWSLAMBDA_ENABLED", "false");
        using var telemetry = this.ConfigureTelemetry();
        using var agent = EnvironmentHelper.GetMockAgent();
        using var process = await RunSampleAndWaitForExit(agent, packageVersion: packageVersion);
#if NETFRAMEWORK
        const int expectedCount = 7;
#else
        const int expectedCount = 4;
#endif
        var spans = (await agent.WaitForSpansAsync(expectedCount)).Where(s => s.Tags.TryGetValue("component", out var component) && component == "aws-sdk").ToList();
        spans.Should().HaveCount(expectedCount);
        var service = EnvironmentHelper.FullSampleName + (schema == "v0" ? "-aws-lambda" : string.Empty);
        ValidateIntegrationSpans(spans, schema, service, isExternalSpan: true);

        var requests = ReadRequests(process.StandardOutput).ToList();
#if NETFRAMEWORK
        requests.Should().HaveCount(11);
#else
        requests.Should().HaveCount(6);
#endif
        foreach (var wire in requests)
        {
            var function = wire.Value<string>("function");
            if (function is "event" or "dry-run")
            {
                wire["clientContext"].Type.Should().Be(JTokenType.Null);
                continue;
            }

            var context = JObject.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(wire.Value<string>("clientContext"))));
            var custom = context["custom"];
            var parentId = ulong.Parse(custom.Value<string>("x-datadog-parent-id"), System.Globalization.CultureInfo.InvariantCulture);
            var span = spans.Should().ContainSingle(s => s.SpanId == parentId && s.Resource == "Lambda.Invoke " + function).Subject;
            custom.Value<string>("x-datadog-trace-id").Should().Be(span.TraceId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            custom.Value<string>("traceparent").Should().NotBeNullOrEmpty();
            wire.Value<string>("payload").Should().Be(function == "failed-invoke" ? "{}" : "{\"message\":\"unchanged\"}");

            if (function == "existing-custom")
            {
                custom.Value<string>("application").Should().Be("value");
            }

            if (function is "existing-custom" or "existing-env")
            {
                context["env"].Value<string>("name").Should().Be("example");
            }
        }

        process.StandardOutput.Should().Contain("EXPECTED_ERROR:failed-invoke");
        var settings = VerifyHelper.GetSpanVerifierSettings();
        // SDK v4 records the expanded request URI; v3 records its resource-path template.
        var sdkSuffix = !string.IsNullOrEmpty(packageVersion) && new Version(packageVersion).Major >= 4 ? ".SdkV4" : string.Empty;
        settings.UseFileName($"{nameof(AwsLambdaSdkTests)}.{schema}{sdkSuffix}");
        settings.DisableRequireUniquePrefix();
        // Sync and async APIs share metadata; both carriers are checked above.
        await VerifyHelper.VerifySpans(spans.GroupBy(s => s.Tags["functionname"]).Select(group => group.First()).ToList(), settings);
        await telemetry.AssertIntegrationEnabledAsync(IntegrationId.AwsLambdaSdk);
    }

    [SkippableTheory]
    [InlineData("DD_TRACE_AWSLAMBDASDK_ENABLED")]
    [InlineData("DD_TRACE_AWSSDK_ENABLED")]
    [Trait("Category", "EndToEnd")]
    public async Task IntegrationDisabled(string disabledKey)
    {
        SetEnvironmentVariable(disabledKey, "false");
        using var agent = EnvironmentHelper.GetMockAgent();
        using var process = await RunSampleAndWaitForExit(agent);
        foreach (var wire in ReadRequests(process.StandardOutput))
        {
            var encoded = wire.Value<string>("clientContext");
            if (encoded is not null)
            {
                Encoding.UTF8.GetString(Convert.FromBase64String(encoded)).Should().NotContain("x-datadog").And.NotContain("traceparent");
            }
        }
    }

    private static IEnumerable<JObject> ReadRequests(string output)
        => output.Split('\n').Where(line => line.StartsWith("WIRE:", StringComparison.Ordinal)).Select(line => JObject.Parse(line.Substring("WIRE:".Length)));
}
