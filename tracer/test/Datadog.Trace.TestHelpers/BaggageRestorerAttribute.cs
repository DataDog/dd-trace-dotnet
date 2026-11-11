// <copyright file="BaggageRestorerAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using Xunit.Sdk;

namespace Datadog.Trace.TestHelpers
{
    /// <summary>
    /// Restores <see cref="Baggage.Current"/> after each test in the decorated class, for tests
    /// that make baggage current rather than passing a <see cref="Baggage"/> instance around.
    /// </summary>
    /// <remarks>
    /// <see cref="Baggage.Current"/> is backed by a static <c>AsyncLocal</c>. A test that writes it
    /// before its first real await does so on the runner's execution context, so without this the
    /// write outlives the test and is visible to whichever test runs next.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public class BaggageRestorerAttribute : BeforeAfterTestAttribute
    {
        private Baggage _baggage;

        public override void Before(MethodInfo methodUnderTest)
        {
            _baggage = Baggage.Current;
            base.Before(methodUnderTest);
        }

        public override void After(MethodInfo methodUnderTest)
        {
            Baggage.Current = _baggage;
            base.After(methodUnderTest);
        }
    }
}
