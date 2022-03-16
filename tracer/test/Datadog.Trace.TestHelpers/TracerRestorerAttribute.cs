// <copyright file="TracerRestorerAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using Datadog.Trace.ClrProfiler;
using Xunit.Sdk;

namespace Datadog.Trace.TestHelpers
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    public class TracerRestorerAttribute : BeforeAfterTestAttribute
    {
        private Tracer _tracer;
        private TracerManager _tracerManager;
        private IDistributedTracer _distributedTracer;
        private bool _isCIVisibilityTags;

        public override void Before(MethodInfo methodUnderTest)
        {
            _tracer = Tracer.Instance;
            _tracerManager = _tracer.TracerManager;
            _distributedTracer = ClrProfiler.DistributedTracer.Instance;
            _isCIVisibilityTags = Tagging.TagsList.IsCiVisibilityAgentless;
            base.Before(methodUnderTest);
        }

        public override void After(MethodInfo methodUnderTest)
        {
            SetTracer(_tracer, _tracerManager);
            ClrProfiler.DistributedTracer.SetInstanceOnlyForTests(_distributedTracer);
            Tagging.TagsList.SetIsCiVisibilityAgentlessMode(_isCIVisibilityTags);
            base.After(methodUnderTest);
        }

        internal static void SetTracer(Tracer tracer, TracerManager manager = null)
        {
            // CI Visibility tracer cannot be replaced, so we use an internal api to ensure the set.
            Tracer.UnsafeSetTracerInstance(tracer);
            manager ??= tracer?.TracerManager;
            if (manager is not null)
            {
                TracerManager.UnsafeReplaceGlobalManager(manager);
            }
        }

        internal static void SetCIVisibilityTags(bool isCIVisibility)
        {
            Tagging.TagsList.SetIsCiVisibilityAgentlessMode(isCIVisibility);
        }
    }
}
