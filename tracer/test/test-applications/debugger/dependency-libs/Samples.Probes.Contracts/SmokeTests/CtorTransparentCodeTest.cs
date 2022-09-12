// <copyright file="CtorTransparentCodeTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Samples.Probes.Contracts.Security;

namespace Samples.Probes.Contracts.SmokeTests
{
    public class CtorTransparentCodeTest : IRun
    {
        public void Run()
        {
            var instance = new SecurityTransparentTest();
        }
    }
}
