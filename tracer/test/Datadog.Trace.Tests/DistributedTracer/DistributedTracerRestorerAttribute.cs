// <copyright file="DistributedTracerRestorerAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using Datadog.Trace.ClrProfiler;
using Xunit.Sdk;

namespace Datadog.Trace.Tests.DistributedTracer
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    internal class DistributedTracerRestorerAttribute : BeforeAfterTestAttribute
    {
        private IDistributedTracer _distributedTracer;

        public override void Before(MethodInfo methodUnderTest)
        {
            _distributedTracer = ClrProfiler.DistributedTracer.Instance;
            base.Before(methodUnderTest);
        }

        public override void After(MethodInfo methodUnderTest)
        {
            ClrProfiler.DistributedTracer.SetInstanceOnlyForTests(_distributedTracer);
            base.After(methodUnderTest);
        }
    }
}
