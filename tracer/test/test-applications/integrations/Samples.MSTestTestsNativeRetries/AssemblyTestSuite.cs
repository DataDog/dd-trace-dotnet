// <copyright file="AssemblyTestSuite.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Samples.MSTestTestsNativeRetries;

[TestClass]
public class AssemblyTestSuite
{
    [AssemblyInitialize]
    public static void Initialize(TestContext context)
    {
        if (Environment.GetEnvironmentVariable("MSTEST_FAIL_ASSEMBLY_INITIALIZE") == "1")
        {
            throw new InvalidOperationException("Assembly initialization failed.");
        }
    }
}
