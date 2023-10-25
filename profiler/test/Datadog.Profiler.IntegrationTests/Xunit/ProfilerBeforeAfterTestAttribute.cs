// <copyright file="ProfilerBeforeAfterTestAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Reflection;
using Xunit.Sdk;

namespace Datadog.Profiler.IntegrationTests.Xunit
{
    internal class ProfilerBeforeAfterTestAttribute : BeforeAfterTestAttribute
    {
        public override void After(MethodInfo methodUnderTest)
        {
            TestContext.Current.TestName = null;
        }

        public override void Before(MethodInfo methodUnderTest)
        {
            TestContext.Current.TestName = methodUnderTest.DeclaringType.Name + "." + methodUnderTest.Name;
        }
    }
}
