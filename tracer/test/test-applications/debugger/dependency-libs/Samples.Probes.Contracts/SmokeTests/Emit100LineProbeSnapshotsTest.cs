// <copyright file="Emit100LineProbeSnapshotsTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Samples.Probes.Contracts.Shared;

namespace Samples.Probes.Contracts.SmokeTests
{
    [LineProbeTestData(lineNumber: 19, unlisted: true, skipOnFramework: new[] { "net6.0" })]
    public class Emit100LineProbeSnapshotsTest : IRun
    {
        public void Run()
        {
            var accu = 0;
            for (int i = 0; i < 100; i++)
            {
                accu += i;
            }

            if (accu > 0)
            {
                throw new IntentionalDebuggerException($"accu is {accu}");
            }
        }
    }
}
