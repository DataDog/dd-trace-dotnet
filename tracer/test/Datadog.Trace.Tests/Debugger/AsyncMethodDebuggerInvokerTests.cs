// <copyright file="AsyncMethodDebuggerInvokerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Tests.Debugger.AsyncMethodProbeResources;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using VerifyTests;
using VerifyXunit;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

[UsesVerify]
public class AsyncMethodDebuggerInvokerTests
{
    public static IEnumerable<object[]> AsyncProbeTests()
    {
        return typeof(IAsyncTestRun).Assembly.GetTypes()
                           .Where(t => t.GetInterface(nameof(IAsyncTestRun)) != null)
                           .Select(t => new object[] { t });
    }

    [SkippableTheory(typeof(Exception), Skip = "Not ready to run, will be addressed in the next async probes PR")]
    [MemberData(nameof(AsyncProbeTests))]
    public async Task Test(Type testType)
    {
        var instance = (IAsyncTestRun)Activator.CreateInstance(testType);
        var snapshots = instance.Run();
        var settings = new VerifySettings();
        settings.ScrubEmptyLines();
        settings.UseParameters(testType);
        VerifierSettings.DerivePathInfo((sourceFile, _, _, _) => new PathInfo(directory: Path.Combine(sourceFile, "..", "snapshots")));
        var toVerify = string.Join(Environment.NewLine, snapshots.Select(snapshot => JToken.Parse(snapshot).ToString(Formatting.Indented)));
        await Verifier.Verify(toVerify, settings);
    }
}
