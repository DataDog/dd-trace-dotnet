// <copyright file="TestContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Threading;

namespace Datadog.Profiler.IntegrationTests.Xunit
{
    internal class TestContext
    {
        private static readonly AsyncLocal<TestContext> _context = new();

        public static TestContext Current => _context.Value ??= new TestContext();

        public string TestName { get; internal set; }
    }
}
