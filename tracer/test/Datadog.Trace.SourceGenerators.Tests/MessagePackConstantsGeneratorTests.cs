// <copyright file="MessagePackConstantsGeneratorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.SourceGenerators.Tests;

public class MessagePackConstantsGeneratorTests
{
    private readonly ITestOutputHelper _output;

    public MessagePackConstantsGeneratorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void CanGenerateSimpleConstant()
    {
        const string input = @"
using Datadog.Trace.SourceGenerators;

namespace MyTests
{
    internal static class TestConstants
    {
        [MessagePackField]
        public const string TestField = ""test_value"";
    }
}";

        var (diagnostics, outputs) = TestHelpers.GetGeneratedTrees<MessagePackConstantsGenerator>(new[] { input }, assertOutput: false);

        using var scope = new AssertionScope();

        // Output diagnostics for debugging
        if (diagnostics.Any())
        {
            _output.WriteLine($"Diagnostics ({diagnostics.Length}):");
            foreach (var diagnostic in diagnostics)
            {
                _output.WriteLine($"  {diagnostic.Id}: {diagnostic.GetMessage()}");
            }
        }
        else
        {
            _output.WriteLine("No diagnostics");
        }

        _output.WriteLine($"Generated {outputs.Length} files");
        foreach (var output in outputs)
        {
            _output.WriteLine("=== Generated File ===");
            _output.WriteLine(output);
            _output.WriteLine(string.Empty);
        }

        outputs.Should().NotBeEmpty();

        // Check if attribute was generated
        outputs.Should().Contain(x => x.Contains("MessagePackFieldAttribute"));

        // Check if constants were generated
        var constantsFile = outputs.FirstOrDefault(x => x.Contains("MessagePackConstants") && !x.Contains("Attribute"));
        constantsFile.Should().NotBeNull("MessagePackConstants class should be generated");
        constantsFile.Should().Contain("TestFieldBytes");
    }

    [Fact]
    public void CanGenerateMultipleConstants()
    {
        const string input = @"
using Datadog.Trace.SourceGenerators;

namespace MyTests
{
    internal static class TestConstants
    {
        [MessagePackField]
        public const string TraceId = ""trace_id"";

        [MessagePackField]
        public const string SpanId = ""span_id"";

        [MessagePackField]
        public const string ServiceName = ""service"";
    }
}";

        var (diagnostics, outputs) = TestHelpers.GetGeneratedTrees<MessagePackConstantsGenerator>(new[] { input }, assertOutput: false);

        using var scope = new AssertionScope();
        diagnostics.Should().BeEmpty();

        var constantsFile = outputs.FirstOrDefault(x => x.Contains("MessagePackConstants") && !x.Contains("Attribute"));
        constantsFile.Should().NotBeNull("MessagePackConstants class should be generated");
        constantsFile.Should().Contain("TraceIdBytes");
        constantsFile.Should().Contain("SpanIdBytes");
        constantsFile.Should().Contain("ServiceNameBytes");
    }

    [Fact]
    public void CanGenerateConstantsFromMultipleClasses()
    {
        const string input1 = @"
using Datadog.Trace.SourceGenerators;

namespace MyTests.Tags
{
    internal static class TagNames
    {
        [MessagePackField]
        public const string Environment = ""env"";

        [MessagePackField]
        public const string Version = ""version"";
    }
}";

        const string input2 = @"
using Datadog.Trace.SourceGenerators;

namespace MyTests.Metrics
{
    internal static class MetricNames
    {
        [MessagePackField]
        public const string SamplingPriority = ""_sampling_priority_v1"";

        [MessagePackField]
        public const string ProcessId = ""process_id"";
    }
}";

        var (diagnostics, outputs) = TestHelpers.GetGeneratedTrees<MessagePackConstantsGenerator>(new[] { input1, input2 }, assertOutput: false);

        using var scope = new AssertionScope();
        diagnostics.Should().BeEmpty();

        var constantsFile = outputs.FirstOrDefault(x => x.Contains("MessagePackConstants") && !x.Contains("Attribute"));
        constantsFile.Should().NotBeNull("MessagePackConstants class should be generated");

        // All constants from both classes should be in the same MessagePackConstants class
        constantsFile.Should().Contain("EnvironmentBytes");
        constantsFile.Should().Contain("VersionBytes");
        constantsFile.Should().Contain("SamplingPriorityBytes");
        constantsFile.Should().Contain("ProcessIdBytes");
    }

    [Fact]
    public void GeneratedBytesMatchMessagePackEncoding()
    {
        const string input = @"
using Datadog.Trace.SourceGenerators;

namespace MyTests
{
    internal static class TestConstants
    {
        [MessagePackField]
        public const string ShortString = ""test"";

        [MessagePackField]
        public const string LongerString = ""trace_id"";
    }
}";

        var (diagnostics, outputs) = TestHelpers.GetGeneratedTrees<MessagePackConstantsGenerator>(new[] { input }, assertOutput: false);

        using var scope = new AssertionScope();
        diagnostics.Should().BeEmpty();

        var constantsFile = outputs.FirstOrDefault(x => x.Contains("MessagePackConstants") && !x.Contains("Attribute"));
        constantsFile.Should().NotBeNull();

        // "test" = 4 bytes -> MessagePack fixstr format: 0xa4 (164) + 4 bytes
        // Expected: [164, 116, 101, 115, 116] = 0xa4 't' 'e' 's' 't'
        constantsFile.Should().Contain("164, 116, 101, 115, 116");

        // "trace_id" = 8 bytes -> MessagePack fixstr format: 0xa8 (168) + 8 bytes
        // Expected: [168, 116, 114, 97, 99, 101, 95, 105, 100]
        constantsFile.Should().Contain("168, 116, 114, 97, 99, 101, 95, 105, 100");
    }

    [Fact]
    public void HandlesNoMarkedFields()
    {
        const string input = @"
using Datadog.Trace.SourceGenerators;

namespace MyTests
{
    internal static class TestConstants
    {
        // No [MessagePackField] attribute
        public const string UnmarkedField = ""test"";
    }
}";

        var (diagnostics, outputs) = TestHelpers.GetGeneratedTrees<MessagePackConstantsGenerator>(new[] { input }, assertOutput: false);

        using var scope = new AssertionScope();
        diagnostics.Should().BeEmpty();

        // Should only generate the attribute file, not the constants file
        outputs.Should().Contain(x => x.Contains("MessagePackFieldAttribute"));

        var constantsFile = outputs.FirstOrDefault(x => x.Contains("MessagePackConstants") && !x.Contains("Attribute"));
        if (constantsFile != null)
        {
            // If constants file is generated, it should be empty (no fields)
            constantsFile.Should().NotContain("UnmarkedFieldBytes");
        }
    }

    [Fact]
    public void ErrorWhenFieldIsNotConst()
    {
        const string input = @"
using Datadog.Trace.SourceGenerators;

namespace MyTests
{
    internal static class TestConstants
    {
        [MessagePackField]
        public static string NotConst = ""test"";
    }
}";

        var (diagnostics, outputs) = TestHelpers.GetGeneratedTrees<MessagePackConstantsGenerator>(new[] { input }, assertOutput: false);

        using var scope = new AssertionScope();

        // Should produce a diagnostic about the field not being const
        diagnostics.Should().NotBeEmpty();
        diagnostics.Should().Contain(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }

    [Fact]
    public void ErrorWhenFieldIsNotString()
    {
        const string input = @"
using Datadog.Trace.SourceGenerators;

namespace MyTests
{
    internal static class TestConstants
    {
        [MessagePackField]
        public const int NotString = 42;
    }
}";

        var (diagnostics, outputs) = TestHelpers.GetGeneratedTrees<MessagePackConstantsGenerator>(new[] { input }, assertOutput: false);

        using var scope = new AssertionScope();

        // Should produce a diagnostic about the field not being a string
        diagnostics.Should().NotBeEmpty();
        diagnostics.Should().Contain(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }

    [Theory]
    [InlineData("\"\"")]
    [InlineData("null")]
    public void ErrorWhenValueIsNullOrEmpty(string value)
    {
        var input = @"
using Datadog.Trace.SourceGenerators;

namespace MyTests
{
    internal static class TestConstants
    {
        [MessagePackField]
        public const string EmptyField = " + value + @";
    }
}";

        var (diagnostics, outputs) = TestHelpers.GetGeneratedTrees<MessagePackConstantsGenerator>(new[] { input }, assertOutput: false);

        using var scope = new AssertionScope();

        // Should produce a diagnostic about the value being null or empty
        diagnostics.Should().NotBeEmpty();
        diagnostics.Should().Contain(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }

    [Fact]
    public void HandlesSpecialCharactersInConstantValues()
    {
        const string input = @"
using Datadog.Trace.SourceGenerators;

namespace MyTests
{
    internal static class TestConstants
    {
        [MessagePackField]
        public const string WithUnderscore = ""_sampling_priority_v1"";

        [MessagePackField]
        public const string WithDots = ""aas.site.name"";

        [MessagePackField]
        public const string WithSlash = ""http/url"";
    }
}";

        var (diagnostics, outputs) = TestHelpers.GetGeneratedTrees<MessagePackConstantsGenerator>(new[] { input }, assertOutput: false);

        using var scope = new AssertionScope();
        diagnostics.Should().BeEmpty();

        var constantsFile = outputs.FirstOrDefault(x => x.Contains("MessagePackConstants") && !x.Contains("Attribute"));
        constantsFile.Should().NotBeNull();
        constantsFile.Should().Contain("WithUnderscoreBytes");
        constantsFile.Should().Contain("WithDotsBytes");
        constantsFile.Should().Contain("WithSlashBytes");

        // Verify the bytes are valid (should contain the MessagePack string format prefix)
        // "_sampling_priority_v1" = 21 chars -> 0xb5 (181) prefix for fixstr
        constantsFile.Should().Contain("181,");
    }

    [Fact]
    public void IgnoresPropertiesWithAttribute()
    {
        const string input = @"
using Datadog.Trace.SourceGenerators;

namespace MyTests
{
    internal static class TestConstants
    {
        [MessagePackField]
        public const string ValidField = ""valid"";

        [MessagePackField]
        public string InvalidProperty { get; } = ""invalid"";
    }
}";

        var (diagnostics, outputs) = TestHelpers.GetGeneratedTrees<MessagePackConstantsGenerator>(new[] { input }, assertOutput: false);

        using var scope = new AssertionScope();

        var constantsFile = outputs.FirstOrDefault(x => x.Contains("MessagePackConstants") && !x.Contains("Attribute"));
        constantsFile.Should().NotBeNull();

        // Should include the valid field
        constantsFile.Should().Contain("ValidFieldBytes");

        // Should not include the property (properties should be ignored, not error)
        constantsFile.Should().NotContain("InvalidPropertyBytes");
    }

    [Fact]
    public void ErrorWhenDuplicateFieldNamesExist()
    {
        const string input1 = @"
using Datadog.Trace.SourceGenerators;

namespace MyTests.Tags
{
    internal static class Tags
    {
        [MessagePackField]
        public const string ProcessTags = ""_dd.tags.process"";
    }
}";

        const string input2 = @"
using Datadog.Trace.SourceGenerators;

namespace MyTests.FieldNames
{
    internal static class MessagePackFieldNames
    {
        [MessagePackField]
        public const string ProcessTags = ""ProcessTags"";
    }
}";

        var (diagnostics, outputs) = TestHelpers.GetGeneratedTrees<MessagePackConstantsGenerator>(new[] { input1, input2 }, assertOutput: false);

        using var scope = new AssertionScope();

        // Output diagnostics for debugging
        _output.WriteLine($"Diagnostics ({diagnostics.Length}):");
        foreach (var diagnostic in diagnostics)
        {
            _output.WriteLine($"  {diagnostic.Id}: {diagnostic.GetMessage()}");
        }

        // Should produce a diagnostic about duplicate field names
        diagnostics.Should().NotBeEmpty();
        diagnostics.Should().Contain(d => d.Id == "DDSG005" && d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        diagnostics.Should().Contain(d => d.GetMessage().Contains("ProcessTags"));

        // Even with duplicates, should still generate the constants file with only the first occurrence
        var constantsFile = outputs.FirstOrDefault(x => x.Contains("MessagePackConstants") && !x.Contains("Attribute"));
        constantsFile.Should().NotBeNull("MessagePackConstants class should be generated even with duplicates");
        constantsFile.Should().Contain("ProcessTagsBytes");

        // Count occurrences of ProcessTagsBytes - should appear three times (comment + ReadOnlySpan + byte[])
        var occurrences = constantsFile!.Split(new[] { "ProcessTagsBytes" }, System.StringSplitOptions.None).Length - 1;
        occurrences.Should().Be(3, "ProcessTagsBytes should appear exactly three times (comment + ReadOnlySpan + byte[] versions)");
    }

    [Fact]
    public void WarnsAboutDuplicatesButGeneratesFirstOccurrence()
    {
        const string input1 = @"
using Datadog.Trace.SourceGenerators;

namespace MyTests.First
{
    internal static class FirstClass
    {
        [MessagePackField]
        public const string SharedName = ""first_value"";
    }
}";

        const string input2 = @"
using Datadog.Trace.SourceGenerators;

namespace MyTests.Second
{
    internal static class SecondClass
    {
        [MessagePackField]
        public const string SharedName = ""second_value"";
    }
}";

        var (diagnostics, outputs) = TestHelpers.GetGeneratedTrees<MessagePackConstantsGenerator>(new[] { input1, input2 }, assertOutput: false);

        using var scope = new AssertionScope();

        // Should have duplicate error
        diagnostics.Should().Contain(d => d.Id == "DDSG005");

        var constantsFile = outputs.FirstOrDefault(x => x.Contains("MessagePackConstants") && !x.Contains("Attribute"));
        constantsFile.Should().NotBeNull();

        // Should contain the first value, not the second
        // "first_value" = 11 chars -> MessagePack fixstr: 0xab (171)
        constantsFile.Should().Contain("171");

        // "second_value" = 12 chars -> MessagePack fixstr: 0xac (172)
        constantsFile.Should().NotContain("172");
    }
}
