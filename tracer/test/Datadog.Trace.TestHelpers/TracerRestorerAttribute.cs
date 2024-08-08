// <copyright file="TracerRestorerAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Internal;
using Xunit.Sdk;

namespace Datadog.Trace.TestHelpers
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public class TracerRestorerAttribute : BeforeAfterTestAttribute
    {
        private InternalTracer _tracer;
        private TracerManager _tracerManager;
        private IDistributedTracer _distributedTracer;

        public override void Before(MethodInfo methodUnderTest)
        {
            _tracer = InternalTracer.Instance;
            _tracerManager = _tracer.TracerManager;
            _distributedTracer = ClrProfiler.DistributedTracer.Instance;
            base.Before(methodUnderTest);
        }

        public override void After(MethodInfo methodUnderTest)
        {
            SetTracer(_tracer, _tracerManager);
            ClrProfiler.DistributedTracer.SetInstanceOnlyForTests(_distributedTracer);
            base.After(methodUnderTest);
        }

        internal static void SetTracer(InternalTracer tracer, TracerManager manager = null)
        {
            // CI Visibility tracer cannot be replaced, so we use an internal api to ensure the set.
            InternalTracer.UnsafeSetTracerInstance(tracer);
            manager ??= tracer?.TracerManager;
            if (manager is not null)
            {
                TracerManager.UnsafeReplaceGlobalManager(manager);
            }
        }
    }
}
