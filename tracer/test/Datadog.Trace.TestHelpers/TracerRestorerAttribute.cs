// <copyright file="TracerRestorerAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using Xunit.Sdk;

namespace Datadog.Trace.TestHelpers
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public class TracerRestorerAttribute : BeforeAfterTestAttribute
    {
        private Tracer _tracer;

        public static void SetTracer(Tracer tracer)
        {
            // CI Visibility tracer cannot be replaced, so we use an internal api to ensure the set.
            Tracer.UnsafeSetTracerInstance(tracer);
        }

        public override void Before(MethodInfo methodUnderTest)
        {
            _tracer = Tracer.Instance;
            base.Before(methodUnderTest);
        }

        public override void After(MethodInfo methodUnderTest)
        {
            SetTracer(_tracer);
            base.After(methodUnderTest);
        }
    }
}
