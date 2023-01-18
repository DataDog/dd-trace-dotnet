// <copyright file="EvidenceTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Iast;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests.IAST.Tainted;

public class EvidenceTests
{
    [Fact]
    public void GivenAnEvidence_WhenGetValueParts_ValuePartsIsCorrect()
    {
        var source = new Source(2, "name", "value");
        source.SetInternalId(0);
        var source2 = new Source(3, "name2", "value2");
        source2.SetInternalId(1);
        var ranges = new Range[] { new Range(0, 2, source), new Range(2, 2, source2) };
        var ev = new Evidence("sql_query", ranges);
        ev.ValueParts.Count.Should().Be(2);
        ev.ValueParts[0].Source.Should().Be(0);
        ev.ValueParts[1].Source.Should().Be(1);
        ev.ValueParts[0].Value.Should().Be("sq");
        ev.ValueParts[1].Value.Should().Be("l_");
    }

    [Fact]
    public void GivenAnEvidence_WhenGetValueParts_ValuePartsIsCorrect2()
    {
        var source = new Source(2, "name", "value");
        source.SetInternalId(0);
        var ranges = new Range[] { new Range(0, 2, source), new Range(2, 2, source) };
        var ev = new Evidence("sql_query", ranges);
        ev.ValueParts.Count.Should().Be(2);
        ev.ValueParts[0].Source.Should().Be(0);
        ev.ValueParts[1].Source.Should().Be(0);
        ev.ValueParts[0].Value.Should().Be("sq");
        ev.ValueParts[1].Value.Should().Be("l_");
    }
}
