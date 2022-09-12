// <copyright file="LambdaSingleLine.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;

namespace Samples.Probes.Contracts.SmokeTests
{
    [LineProbeTestData(lineNumber: 15, unlisted: true)]
    public class LambdaSingleLine : IRun
    {
        public void Run()
        {
            var q = Enumerable.Range(1, 10).Where(i => i % 2 == 0).Select(f => f * f).ToList();
        }
    }
}
