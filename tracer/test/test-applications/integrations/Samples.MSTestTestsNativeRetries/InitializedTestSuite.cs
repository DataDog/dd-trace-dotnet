// <copyright file="InitializedTestSuite.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Samples.MSTestTestsNativeRetries;

[TestClass]
[Retry(2)]
public class InitializedTestSuite
{
    [ClassInitialize]
    public static async Task Initialize(TestContext context)
    {
        await Task.Yield();
        if (Environment.GetEnvironmentVariable("MSTEST_FAIL_CLASS_INITIALIZE") == "1")
        {
            throw new InvalidOperationException("Class initialization failed.");
        }
    }

    [TestMethod]
    public async Task PassesAfterClassInitialization()
    {
        await Task.Yield();
        Assert.IsTrue(TestSuite.RecordAttempt(nameof(PassesAfterClassInitialization)) >= 2);
    }

    [TestMethod]
    public async Task AnotherTestAfterClassInitialization()
    {
        await Task.Yield();
        Assert.IsTrue(TestSuite.RecordAttempt(nameof(AnotherTestAfterClassInitialization)) >= 2);
    }
}
