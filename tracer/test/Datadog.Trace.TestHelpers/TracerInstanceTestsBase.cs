// <copyright file="TracerInstanceTestsBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using NUnit.Framework;

namespace Datadog.Trace.TestHelpers
{
    /// <summary>
    /// Base class for tests that need to override Tracer.Instance
    /// </summary>
    [NonParallelizable]
    public abstract class TracerInstanceTestsBase
    {
        private Tracer _tracer;

        [SetUp]
        public void Before()
        {
            _tracer = Tracer.Instance;
        }

        [TearDown]
        public void After()
        {
            SetTracer(_tracer);
        }

        protected void SetTracer(Tracer tracer)
        {
            // CI Visibility tracer cannot be replaced, so we use an internal api to ensure the set.
            Tracer.UnsafeSetTracerInstance(tracer);
        }
    }
}
