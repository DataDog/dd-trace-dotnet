// <copyright file="AspNetCore5Reliability.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Mime;
using System.Security.Policy;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast.Telemetry;
using Datadog.Trace.Security.IntegrationTests.IAST;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using VerifyTests;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Reliability;

public class AspNetCore5Reliability : AspNetBase, IClassFixture<AspNetCoreTestFixture>
{
    public AspNetCore5Reliability(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : base("AspNetCore5", outputHelper, "/shutdown", testName: "AspNetCore5.SecurityEnabled")
    {
        EnableRasp();
        SetSecurity(true);
        EnableIast(true);
        OutputHelper = outputHelper;
    }

    public ITestOutputHelper OutputHelper { get; }

    public override void Dispose()
    {
        base.Dispose();
    }

    public async Task<(AspNetCoreTestFixture Fixture, List<Task> Requests)> TryTestApp(ManualResetEventSlim fence)
    {
        var fixture = new AspNetCoreTestFixture();
        fixture.SetOutput(OutputHelper);
        await fixture.TryStartApp(this, true);
        var agent = fixture.Agent;
        var port = fixture.HttpPort;
        string[] urls =
            {
            "/",
            "/Iast/WeakHashing",
            "/Iast/ExecuteCommand?file=nonexisting.exe&argumentLine=arg1",
            "/Iast/GetFileContent?file=/nonexisting.txt",
            "/Iast/InsecureCookie",
            "/Iast/NoHttpOnlyCookie",
            "/Iast/NoSameSiteCookie",
            "/Iast/AllVulnerabilitiesCookie",
            "/Iast/Ssrf?url=http://www.google.com",
            "/Iast/Ldap?path=LDAP://ldap.forumsys.com:389/dc=example,dc=com",
            "/Iast/WeakRandomness",
            "/Iast/TBV?name=name&value=value",
            "/Iast/UnvalidatedRedirect?param=value",
            "/Iast/StackTraceLeak",
            "/Iast/XpathInjection?user=James&password=Smith",
            "/Iast/TypeReflectionInjection/?type=System.String",
            "/Iast/NewtonsoftJsonParseTainting?json=%7B%22key%22%3A%20%22value%22%7D",
            "/Iast/ReflectedXss/?param=<script>alert('Injection!')</script>",
            "/Iast/ReflectedXssEscaped/?param=<script>alert('Injection!')</script>",
            "/Iast/ReflectedXssEscaped/?param=Normal Texxt",
            "/Iast/JsonParseTainting?json=%7B%22key%22%3A%20%22value%22%7D",
            "/Iast/CustomAttribute?userName=test",
            "/Iast/CustomManual?userName=test",
            };
        var time = DateTime.UtcNow;

        var tasks = new List<Task>();
        foreach (var url in urls)
        {
            tasks.Add(Task.Run(async () =>
            {
                fence.Wait();
                var res = await SubmitRequest(port, url, null, null, null, null);
            }));
        }

        return (fixture, tasks);
    }

    [SkippableFact]
    public async Task TestMultipleInstances()
    {
        using var fence = new ManualResetEventSlim(false);

        var tasks = new List<Task<(AspNetCoreTestFixture Fixture, List<Task> Requests)>>();
        try
        {
            for (var i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(async () => await TryTestApp(fence)));
            }

            // Release all requests at the same time
            fence.Set();

            // Wait for all requests
            var requests = tasks.SelectMany(f => f.Result.Requests).ToArray();
            await Task.WhenAll(requests);

            // Wait for all fixtures
            await Task.WhenAll(tasks.ToArray());
        }
        finally
        {
            var fixtures = tasks.Select(f => f.Result.Fixture).ToList();
            fixtures.AsParallel().ForAll(f => f.CloseProcess().Should().Be(0));
            fixtures.AsParallel().ForAll(f => f.Dispose());
        }
    }
}
#endif
