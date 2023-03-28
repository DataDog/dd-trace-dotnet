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
        ev.ValueParts.Count.Should().Be(3);
        ev.ValueParts[0].Source.Should().Be(0);
        ev.ValueParts[1].Source.Should().Be(1);
        ev.ValueParts[2].Source.Should().BeNull();
        ev.ValueParts[0].Value.Should().Be("sq");
        ev.ValueParts[1].Value.Should().Be("l_");
        ev.ValueParts[2].Value.Should().Be("query");
    }

    [Fact]
    public void GivenAnEvidence_WhenGetValueParts_ValuePartsIsCorrect2()
    {
        var source = new Source(2, "name", "value");
        source.SetInternalId(0);
        var ranges = new Range[] { new Range(2, 2, source), new Range(6, 2, source) };
        var ev = new Evidence("sql_query", ranges);
        ev.ValueParts.Count.Should().Be(5);
        ev.ValueParts[0].Source.Should().BeNull();
        ev.ValueParts[1].Source.Should().Be(0);
        ev.ValueParts[2].Source.Should().BeNull();
        ev.ValueParts[3].Source.Should().Be(0);
        ev.ValueParts[4].Source.Should().BeNull();
        ev.ValueParts[0].Value.Should().Be("sq");
        ev.ValueParts[1].Value.Should().Be("l_");
        ev.ValueParts[2].Value.Should().Be("qu");
        ev.ValueParts[3].Value.Should().Be("er");
        ev.ValueParts[4].Value.Should().Be("y");
    }

    [Fact]
    public void GivenAnEvidence_WhenGetValueParts_ValuePartsIsCorrect3()
    {
        var ev = new Evidence("sql_query", null);
        ev.ValueParts.Should().BeNull();
    }

    [Fact]
    public void GivenAnEvidence_WhenGetValueParts_ValuePartsIsCorrect4()
    {
        var source = new Source(2, "name", "value");
        source.SetInternalId(0);
        Range emptyRange = new(0, 0, source);
        var ranges = new Range[] { emptyRange };
        var ev = new Evidence("sql_query", ranges);
        ev.ValueParts.Should().BeNull();
    }

    [Fact]
    public void GivenAnEvidence_WhenGetValueParts_ValuePartsIsCorrect5()
    {
        var ranges = new Range[] { new Range(0, 2, null), new Range(2, 2, null) };
        var ev = new Evidence("sql_query", ranges);
        ev.ValueParts.Should().BeNull();
    }

    [Fact]
    public void GivenAnEvidence_WhenGetValueParts_ValuePartsIsCorrect6()
    {
        var source = new Source(2, "name", "value");
        source.SetInternalId(0);
        var ranges = new Range[] { new Range(0, 2, null), new Range(2, 2, source) };
        var ev = new Evidence("sql_query", ranges);
        ev.ValueParts.Count.Should().Be(3);
        ev.ValueParts[0].Source.Should().BeNull();
        ev.ValueParts[1].Source.Should().Be(0);
        ev.ValueParts[2].Source.Should().BeNull();
        ev.ValueParts[0].Value.Should().Be("sq");
        ev.ValueParts[1].Value.Should().Be("l_");
        ev.ValueParts[2].Value.Should().Be("query");
    }

    [Fact]
    public void GivenAnEvidence_WhenGetValueParts_ValuePartsIsCorrect7()
    {
        var source = new Source(2, "name", "value");
        source.SetInternalId(0);
        var source2 = new Source(3, "name2", "value2");
        source2.SetInternalId(1);
        var ranges = new Range[] { new Range(1, 1, source), new Range(2, 2, source2) };
        var ev = new Evidence("sql_query", ranges);
        ev.ValueParts.Count.Should().Be(4);
        ev.ValueParts[0].Source.Should().BeNull();
        ev.ValueParts[1].Source.Should().Be(0);
        ev.ValueParts[2].Source.Should().Be(1);
        ev.ValueParts[3].Source.Should().BeNull();
        ev.ValueParts[0].Value.Should().Be("s");
        ev.ValueParts[1].Value.Should().Be("q");
        ev.ValueParts[2].Value.Should().Be("l_");
        ev.ValueParts[3].Value.Should().Be("query");
    }

    [Fact]
    public void GivenAnEvidence_WhenGetValueParts_ValuePartsIsCorrect8()
    {
        var source = new Source(2, "name", "value");
        source.SetInternalId(0);
        var source2 = new Source(3, "name2", "value2");
        source2.SetInternalId(1);
        var ranges = new Range[] { new Range(0, 5, source), new Range(5, 4, source2) };
        var ev = new Evidence("sql_query", ranges);
        ev.ValueParts.Count.Should().Be(2);
        ev.ValueParts[0].Source.Should().Be(0);
        ev.ValueParts[1].Source.Should().Be(1);
        ev.ValueParts[0].Value.Should().Be("sql_q");
        ev.ValueParts[1].Value.Should().Be("uery");
    }

    [Fact]
    public void GivenAnEvidence_WhenGetValueParts_ValuePartsIsCorrect9()
    {
        var source = new Source(2, "name", "value");
        source.SetInternalId(0);
        var source2 = new Source(3, "name2", "value2");
        source2.SetInternalId(1);
        var ranges = new Range[] { new Range(6, 44, source2), new Range(0, 5, source) };
        var ev = new Evidence("sql_query", ranges);
        ev.ValueParts.Count.Should().Be(2);
        ev.ValueParts[0].Source.Should().Be(0);
        ev.ValueParts[1].Source.Should().BeNull();
        ev.ValueParts[0].Value.Should().Be("sql_q");
        ev.ValueParts[1].Value.Should().Be("uery");
    }

    [Fact]
    public void GivenAnEvidence_WhenGetValueParts_ValuePartsIsCorrect10()
    {
        var source = new Source(2, "name", "value");
        source.SetInternalId(0);
        var source2 = new Source(3, "name2", "value2");
        source2.SetInternalId(1);
        var ranges = new Range[] { new Range(0, 5, source), new Range(1, 2, source2) };
        var ev = new Evidence("sql_query", ranges);
        ev.ValueParts.Count.Should().Be(2);
        ev.ValueParts[0].Source.Should().Be(0);
        ev.ValueParts[1].Source.Should().BeNull();
        ev.ValueParts[0].Value.Should().Be("sql_q");
        ev.ValueParts[1].Value.Should().Be("uery");
    }

    [Fact]
    public void GivenAnEvidence_WhenGetValueParts_ValuePartsIsCorrect11()
    {
        var source = new Source(2, "name", "value");
        source.SetInternalId(0);
        var source2 = new Source(3, "name2", "value2");
        source2.SetInternalId(1);
        var ranges = new Range[] { new Range(0, 5, source), new Range(6, 0, source2) };
        var ev = new Evidence("sql_query", ranges);
        ev.ValueParts.Count.Should().Be(2);
        ev.ValueParts[0].Source.Should().Be(0);
        ev.ValueParts[1].Source.Should().BeNull();
        ev.ValueParts[0].Value.Should().Be("sql_q");
        ev.ValueParts[1].Value.Should().Be("uery");
    }

    [Fact]
    public void GivenAnEvidence_WhenGetValueParts_ValuePartsIsCorrect12()
    {
        var source = new Source(2, "name", "value");
        source.SetInternalId(0);
        var source2 = new Source(3, "name2", "value2");
        source2.SetInternalId(1);
        var ranges = new Range[] { new Range(0, 5, source), new Range(6, 1, null) };
        var ev = new Evidence("sql_query", ranges);
        ev.ValueParts.Count.Should().Be(2);
        ev.ValueParts[0].Source.Should().Be(0);
        ev.ValueParts[1].Source.Should().BeNull();
        ev.ValueParts[0].Value.Should().Be("sql_q");
        ev.ValueParts[1].Value.Should().Be("uery");
    }

    [Fact]
    public void GivenAnEvidence_WhenGetValueParts_ValuePartsIsCorrect13()
    {
        var source = new Source(2, "name", "value");
        source.SetInternalId(0);
        var source2 = new Source(3, "name2", "value2");
        source2.SetInternalId(1);
        var ranges = new Range[] { new Range(0, 3, source), new Range(2, 2, source2) };
        var ev = new Evidence("sql_query", ranges);
        ev.ValueParts.Count.Should().Be(2);
        ev.ValueParts[0].Source.Should().Be(0);
        ev.ValueParts[1].Source.Should().BeNull();
        ev.ValueParts[0].Value.Should().Be("sql");
        ev.ValueParts[1].Value.Should().Be("_query");
    }
}
