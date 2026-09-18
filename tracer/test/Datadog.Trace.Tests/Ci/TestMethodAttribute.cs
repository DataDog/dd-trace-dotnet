// <copyright file="TestMethodAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Threading.Tasks;

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    internal class TestMethodAttribute
    {
        public virtual TestResult[] Execute(ITestMethod testMethod) => [];

        internal virtual Task<TestResult[]> ExecuteAsync(ITestMethod testMethod) => Task.FromResult(Execute(testMethod));
    }
}
