// <copyright file="StringHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Tools.Analyzers.LogAnalyzer.Helpers;
using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Datadog.Trace.Tools.Analyzers.Tests.LogAnalyzer;

public class StringHelperTests
{
    [Fact]
    public void Locations()
    {
        string testStr = """
        "text text \"X\" text"
        """;
        int remappedLocation = StringHelper.GetPositionInLiteral(testStr, GetXPosition(testStr) + 1);

        remappedLocation.Should().Be(testStr.IndexOf('X') + 1);
    }

    [Fact]
    public void TestExactMappingInVerbatimLiteralWithEscapes()
    {
        string testStr = """
        @"text ""text"" text X text"
        """;
        int remappedLocation = StringHelper.GetPositionInLiteral(testStr, GetXPosition(testStr));

        remappedLocation.Should().Be(testStr.IndexOf('X'));
    }

    [Fact]
    public void TestExactMappingInLiteralWithUtf16Escape()
    {
        string testStr = """
        "text \u0000 text X text"
        """;
        int remappedLocation = StringHelper.GetPositionInLiteral(testStr, GetXPosition(testStr));

        remappedLocation.Should().Be(testStr.IndexOf('X'));
    }

    [Fact]
    public void TestExactMappingInLiteralWithUtf16SurrogateEscape()
    {
        string testStr = """
        "text \U00000000 text X text"
        """;
        testStr.Should().Be("\"text \\U00000000 text X text\"");
        int remappedLocation = StringHelper.GetPositionInLiteral(testStr, GetXPosition(testStr));

        remappedLocation.Should().Be(testStr.IndexOf('X'));
    }

    [Fact]
    public void TestExactMappingInLiteralWithHexEscape4Chars()
    {
        string testStr = "\"text \\x1111 text X text\"";
        int remappedLocation = StringHelper.GetPositionInLiteral(testStr, GetXPosition(testStr));

        remappedLocation.Should().Be(testStr.IndexOf('X'));
    }

    [Fact]
    public void TestExactMappingInLiteralWithHexEscape()
    {
        string testStr = "\"text \\x01 text X text\"";
        int remappedLocation = StringHelper.GetPositionInLiteral(testStr, GetXPosition(testStr));

        remappedLocation.Should().Be(testStr.IndexOf('X'));
    }

    [Fact]
    public void TestExactMappingInLiteralWithHexEscapeWithoutSpace()
    {
        string testStr = "\"text \\x1l text X text\"";
        int remappedLocation = StringHelper.GetPositionInLiteral(testStr, GetXPosition(testStr));

        remappedLocation.Should().Be(testStr.IndexOf('X'));
    }

    [Fact]
    public void TestExactMappingInLiteralWithTab()
    {
        string testStr = "\"text \\t text X text\"";
        int remappedLocation = StringHelper.GetPositionInLiteral(testStr, GetXPosition(testStr));

        remappedLocation.Should().Be(testStr.IndexOf('X'));
    }

    [Fact]
    public void TestExactMappingInLiteralAtTheStartSurrounded()
    {
        string testStr = "\"\\tX\t\"";
        int remappedLocation = StringHelper.GetPositionInLiteral(testStr, GetXPosition(testStr));

        remappedLocation.Should().Be(testStr.IndexOf('X'));
    }

    private static int GetXPosition(string literal)
    {
        var literalExpression = SyntaxFactory.ParseExpression(literal) as LiteralExpressionSyntax;
        return literalExpression.Token.ValueText.IndexOf('X');
    }
}
