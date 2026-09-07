// <copyright file="RetryLifecycleTestSuite.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Samples.MSTestTestsNativeRetries;

[TestClass]
[DoNotParallelize]
[TestCategory("CustomRetry")]
public class RetryLifecycleTestSuite : IDisposable
{
    private static readonly HashSet<RetryLifecycleTestSuite> Instances = new();
    private static MemoryStream _sharedResource;
    private static int _cleanupCount;
    private static int _disposeCount;
    private int _instanceValue;

    public TestContext TestContext { get; set; }

    [ClassInitialize]
    public static async Task InitializeClass(TestContext context)
    {
        await Task.Yield();
        _sharedResource = new MemoryStream();
    }

    [TestInitialize]
    public void InitializeTest()
    {
        Assert.IsTrue(Instances.Add(this), "Each attempt must receive a new test class instance.");
        Assert.AreEqual(0, _instanceValue, "The previous attempt's instance state must not survive.");
        Assert.IsTrue(_sharedResource.CanWrite, "ClassCleanup must run after all retries.");
#pragma warning disable MSTESTEXP // Verify the ambient TestContext used by MSTest 4.4 retries.
        Assert.AreSame(TestContext, TestContext.Current);
#pragma warning restore MSTESTEXP
        _instanceValue = 1;
    }

    [TestMethod]
    [Retry(2)]
    public void RetriesUseFreshInstances()
    {
        _instanceValue++;
        Assert.IsTrue(TestSuite.RecordAttempt(nameof(RetriesUseFreshInstances)) >= 4);
    }

    [TestMethod]
    [Retry(2)]
    public void InitiallyPassesWithFreshInstances()
    {
        _instanceValue++;
        TestSuite.RecordAttempt(nameof(InitiallyPassesWithFreshInstances));
    }

    [TestCleanup]
    public void CleanupTest()
    {
        Assert.AreEqual(2, _instanceValue);
        _instanceValue = -1;
        _cleanupCount++;
    }

    public void Dispose()
    {
        Assert.AreEqual(-1, _instanceValue, "MSTest must run TestCleanup before disposing the instance.");
        _disposeCount++;
    }

    [ClassCleanup]
    public static void CleanupClass()
    {
        Assert.AreEqual(Instances.Count, _cleanupCount);
        Assert.AreEqual(Instances.Count, _disposeCount);
        _sharedResource.Dispose();
    }
}
