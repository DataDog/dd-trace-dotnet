// <copyright file="TracerInstanceTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Ci;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests
{
    [CollectionDefinition(nameof(TracerInstanceTest), DisableParallelization = true)]
    [TracerRestorer]
    public class TracerInstanceTest
    {
        [Fact]
        public void NormalTracerInstanceSwap()
        {
            var tracerOne = TracerHelper.Create();
            var tracerTwo = TracerHelper.Create();

            TracerRestorerAttribute.SetTracer(tracerOne);
            Tracer.Instance.Should().Be(tracerOne);
            Tracer.Instance.TracerManager.Should().Be(tracerOne.TracerManager);

            TracerRestorerAttribute.SetTracer(tracerTwo);
            Tracer.Instance.Should().Be(tracerTwo);
            Tracer.Instance.TracerManager.Should().Be(tracerTwo.TracerManager);

            TracerRestorerAttribute.SetTracer(null);
            Tracer.Instance.Should().BeNull();
        }

        [Fact]
        public void LockedTracerInstanceSwap()
        {
            var tracerOne = TracerHelper.Create();
            var tracerTwo = new LockedTracer();

            TracerRestorerAttribute.SetTracer(tracerOne);
            Tracer.Instance.Should().Be(tracerOne);
            Tracer.Instance.TracerManager.Should().Be(tracerOne.TracerManager);

            TracerRestorerAttribute.SetTracer(null);
            Tracer.Instance.Should().BeNull();

            // Set the locked tracer
            TracerRestorerAttribute.SetTracer(tracerTwo);
            Tracer.Instance.Should().Be(tracerTwo);
            Tracer.Instance.TracerManager.Should().Be(tracerTwo.TracerManager);

            // We test the locked tracer cannot be replaced.
#pragma warning disable CS0618 // Setter isn't actually obsolete, just should be internal
            Assert.Throws<InvalidOperationException>(() => Tracer.Instance = tracerOne);

            Assert.Throws<InvalidOperationException>(() => Tracer.Instance = null);

            Assert.Throws<InvalidOperationException>(() => TracerManager.ReplaceGlobalManager(null, TracerManagerFactory.Instance));
            Assert.Throws<InvalidOperationException>(() => TracerManager.ReplaceGlobalManager(null, new CITracerManagerFactory()));
        }

        private class LockedTracer : Tracer
        {
            internal LockedTracer()
                : base(new LockedTracerManager())
            {
            }
        }

        private class LockedTracerManager : TracerManager, ILockedTracer
        {
            public LockedTracerManager()
                : base(null, null, null, null, null, null, null, null)
            {
            }
        }
    }
}
