// <copyright file="MethodMetadataCollectionTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Reflection;
using Datadog.Trace.Debugger.Instrumentation.Collections;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

public class MethodMetadataCollectionTests
{
    [Fact]
    public void Update_IsKeptWhenTheCollectionGrows()
    {
        var collection = new MethodMetadataCollection();
        Create(collection, 0);

        // UpdateProbeInfo runs Get(index).Update(...), and another method's first hit can grow the collection in between.
        ref var info = ref collection.Get(0);
        Create(collection, 1000);
        info.Update(["probe-1"], [3], instrumentationVersion: 1);

        var probes = collection.Get(0).Probes;
        probes.InstrumentationVersion.Should().Be(1);
        probes.ProbeIds.Should().Equal("probe-1");
        probes.ProbeMetadataIndices.Should().Equal(3);
    }

    [Fact]
    public void Update_DoesNotChangeTheProbesAReaderAlreadyHas()
    {
        var collection = new MethodMetadataCollection();
        Create(collection, 0);
        var info = collection.Get(0);
        info.Probes.InstrumentationVersion.Should().Be(-1);
        info.Probes.ProbeIds.Should().BeEmpty();

        info.Update(["probe-1", "probe-2"], [3, 4], instrumentationVersion: 1);
        var probes = info.Probes;
        info.Update(["probe-1"], [3], instrumentationVersion: 2);

        probes.ProbeIds.Should().Equal("probe-1", "probe-2");
        probes.ProbeMetadataIndices.Should().Equal(3, 4);
        probes.InstrumentationVersion.Should().Be(1);
        info.Probes.InstrumentationVersion.Should().Be(2);
    }

    private static void Create(MethodMetadataCollection collection, int index)
    {
        var method = MethodBase.GetCurrentMethod();
        collection.TryCreateNonAsyncMethodMetadataIfNotExists(index, method.MethodHandle, method.DeclaringType.TypeHandle).Should().BeTrue();
    }
}
