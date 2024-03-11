// <copyright file="TagsListGeneratorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators.TagsListGenerator;
using Datadog.Trace.SourceGenerators.TagsListGenerator.Diagnostics;
using Xunit;

namespace Datadog.Trace.SourceGenerators.Tests
{
    public class TagsListGeneratorTests
    {
        [Fact]
        public void CanGenerateTagsListWithTag()
        {
            const string input = @"using Datadog.Trace.SourceGenerators;
namespace MyTests.TestListNameSpace
{
    public class TestList 
    { 
        [Tag(""TestId"")]
    	public string Id { get; set; }
    }
}";
            const string expected = Constants.FileHeader + @"using Datadog.Trace.Processors;
using Datadog.Trace.Tagging;
using System;

namespace MyTests.TestListNameSpace
{
    partial class TestList
    {
        // IdBytes = MessagePack.Serialize(""TestId"");
        private static ReadOnlySpan<byte> IdBytes => new byte[] { 166, 84, 101, 115, 116, 73, 100 };

        public override string? GetTag(string key)
        {
            return key switch
            {
                ""TestId"" => Id,
                _ => base.GetTag(key),
            };
        }

        public override void SetTag(string key, string value)
        {
            switch(key)
            {
                case ""TestId"": 
                    Id = value;
                    break;
                default: 
                    base.SetTag(key, value);
                    break;
            }
        }

        public override void EnumerateTags<TProcessor>(ref TProcessor processor)
        {
            if (Id is not null)
            {
                processor.Process(new TagItem<string>(""TestId"", Id, IdBytes));
            }

            base.EnumerateTags(ref processor);
        }

        protected override void WriteAdditionalTags(System.Text.StringBuilder sb)
        {
            if (Id is not null)
            {
                sb.Append(""TestId (tag):"")
                  .Append(Id)
                  .Append(',');
            }

            base.WriteAdditionalTags(sb);
        }
    }
}
";

            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<TagListGenerator>(input);
            Assert.Equal(expected, output);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public void CanGenerateTagsListWithMetric()
        {
            const string input = @"using Datadog.Trace.SourceGenerators;
namespace MyTests.TestListNameSpace
{
    public class TestList 
    { 
        [Metric(""TestId"")]
    	public double? Id { get; set; }
    }
}";
            const string expected = Constants.FileHeader + @"using Datadog.Trace.Processors;
using Datadog.Trace.Tagging;
using System;

namespace MyTests.TestListNameSpace
{
    partial class TestList
    {
        // IdBytes = MessagePack.Serialize(""TestId"");
        private static ReadOnlySpan<byte> IdBytes => new byte[] { 166, 84, 101, 115, 116, 73, 100 };

        public override double? GetMetric(string key)
        {
            return key switch
            {
                ""TestId"" => Id,
                _ => base.GetMetric(key),
            };
        }

        public override void SetMetric(string key, double? value)
        {
            switch(key)
            {
                case ""TestId"": 
                    Id = value;
                    break;
                default: 
                    base.SetMetric(key, value);
                    break;
            }
        }

        public override void EnumerateMetrics<TProcessor>(ref TProcessor processor)
        {
            if (Id is not null)
            {
                processor.Process(new TagItem<double>(""TestId"", Id.Value, IdBytes));
            }

            base.EnumerateMetrics(ref processor);
        }

        protected override void WriteAdditionalMetrics(System.Text.StringBuilder sb)
        {
            if (Id is not null)
            {
                sb.Append(""TestId (metric):"")
                  .Append(Id.Value)
                  .Append(',');
            }

            base.WriteAdditionalMetrics(sb);
        }
    }
}
";

            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<TagListGenerator>(input);
            Assert.Equal(expected, output);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public void CanGenerateTagsListWithMultipleTags()
        {
            const string input = @"using Datadog.Trace.SourceGenerators;
namespace MyTests.TestListNameSpace
{
    public class TestList 
    { 
        [Tag(""IdTag"")]
    	public string Id { get; set; }

        [Tag(""NameTag"")]
    	public string Name { get; set; }
    }
}";
            const string expected = Constants.FileHeader + @"using Datadog.Trace.Processors;
using Datadog.Trace.Tagging;
using System;

namespace MyTests.TestListNameSpace
{
    partial class TestList
    {
        // IdBytes = MessagePack.Serialize(""IdTag"");
        private static ReadOnlySpan<byte> IdBytes => new byte[] { 165, 73, 100, 84, 97, 103 };
        // NameBytes = MessagePack.Serialize(""NameTag"");
        private static ReadOnlySpan<byte> NameBytes => new byte[] { 167, 78, 97, 109, 101, 84, 97, 103 };

        public override string? GetTag(string key)
        {
            return key switch
            {
                ""IdTag"" => Id,
                ""NameTag"" => Name,
                _ => base.GetTag(key),
            };
        }

        public override void SetTag(string key, string value)
        {
            switch(key)
            {
                case ""IdTag"": 
                    Id = value;
                    break;
                case ""NameTag"": 
                    Name = value;
                    break;
                default: 
                    base.SetTag(key, value);
                    break;
            }
        }

        public override void EnumerateTags<TProcessor>(ref TProcessor processor)
        {
            if (Id is not null)
            {
                processor.Process(new TagItem<string>(""IdTag"", Id, IdBytes));
            }

            if (Name is not null)
            {
                processor.Process(new TagItem<string>(""NameTag"", Name, NameBytes));
            }

            base.EnumerateTags(ref processor);
        }

        protected override void WriteAdditionalTags(System.Text.StringBuilder sb)
        {
            if (Id is not null)
            {
                sb.Append(""IdTag (tag):"")
                  .Append(Id)
                  .Append(',');
            }

            if (Name is not null)
            {
                sb.Append(""NameTag (tag):"")
                  .Append(Name)
                  .Append(',');
            }

            base.WriteAdditionalTags(sb);
        }
    }
}
";
            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<TagListGenerator>(input);
            Assert.Equal(expected, output);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public void CanGenerateTagsListWithMultipleMetrics()
        {
            const string input = @"using Datadog.Trace.SourceGenerators;
namespace MyTests.TestListNameSpace
{
    public class TestList 
    { 
        [Metric(""IdMetric"")]
    	public double? Id { get; set; }

        [Metric(""NameMetric"")]
    	public double? Name { get; set; }
    }
}";
            const string expected = Constants.FileHeader + @"using Datadog.Trace.Processors;
using Datadog.Trace.Tagging;
using System;

namespace MyTests.TestListNameSpace
{
    partial class TestList
    {
        // IdBytes = MessagePack.Serialize(""IdMetric"");
        private static ReadOnlySpan<byte> IdBytes => new byte[] { 168, 73, 100, 77, 101, 116, 114, 105, 99 };
        // NameBytes = MessagePack.Serialize(""NameMetric"");
        private static ReadOnlySpan<byte> NameBytes => new byte[] { 170, 78, 97, 109, 101, 77, 101, 116, 114, 105, 99 };

        public override double? GetMetric(string key)
        {
            return key switch
            {
                ""IdMetric"" => Id,
                ""NameMetric"" => Name,
                _ => base.GetMetric(key),
            };
        }

        public override void SetMetric(string key, double? value)
        {
            switch(key)
            {
                case ""IdMetric"": 
                    Id = value;
                    break;
                case ""NameMetric"": 
                    Name = value;
                    break;
                default: 
                    base.SetMetric(key, value);
                    break;
            }
        }

        public override void EnumerateMetrics<TProcessor>(ref TProcessor processor)
        {
            if (Id is not null)
            {
                processor.Process(new TagItem<double>(""IdMetric"", Id.Value, IdBytes));
            }

            if (Name is not null)
            {
                processor.Process(new TagItem<double>(""NameMetric"", Name.Value, NameBytes));
            }

            base.EnumerateMetrics(ref processor);
        }

        protected override void WriteAdditionalMetrics(System.Text.StringBuilder sb)
        {
            if (Id is not null)
            {
                sb.Append(""IdMetric (metric):"")
                  .Append(Id.Value)
                  .Append(',');
            }

            if (Name is not null)
            {
                sb.Append(""NameMetric (metric):"")
                  .Append(Name.Value)
                  .Append(',');
            }

            base.WriteAdditionalMetrics(sb);
        }
    }
}
";
            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<TagListGenerator>(input);
            Assert.Equal(expected, output);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public void CanGenerateTagsListForReadOnlyTag()
        {
            const string input = @"using Datadog.Trace.SourceGenerators;
namespace MyTests.TestListNameSpace
{
    public class TestList 
    { 
        [Tag(""IdTag"")]
    	public string Id { get; } = ""Some Value"";

        [Tag(""TestId"")]
    	public string Test { get; set; };

        [Tag(""NameTag"")]
    	public string Name => ""Some Name"";
    }
}";
            const string expected = Constants.FileHeader + @"using Datadog.Trace.Processors;
using Datadog.Trace.Tagging;
using System;

namespace MyTests.TestListNameSpace
{
    partial class TestList
    {
        // IdBytes = MessagePack.Serialize(""IdTag"");
        private static ReadOnlySpan<byte> IdBytes => new byte[] { 165, 73, 100, 84, 97, 103 };
        // TestBytes = MessagePack.Serialize(""TestId"");
        private static ReadOnlySpan<byte> TestBytes => new byte[] { 166, 84, 101, 115, 116, 73, 100 };
        // NameBytes = MessagePack.Serialize(""NameTag"");
        private static ReadOnlySpan<byte> NameBytes => new byte[] { 167, 78, 97, 109, 101, 84, 97, 103 };

        public override string? GetTag(string key)
        {
            return key switch
            {
                ""IdTag"" => Id,
                ""TestId"" => Test,
                ""NameTag"" => Name,
                _ => base.GetTag(key),
            };
        }

        public override void SetTag(string key, string value)
        {
            switch(key)
            {
                case ""TestId"": 
                    Test = value;
                    break;
                case ""IdTag"": 
                case ""NameTag"": 
                    Logger.Value.Warning(""Attempted to set readonly tag {TagName} on {TagType}. Ignoring."", key, nameof(TestList));
                    break;
                default: 
                    base.SetTag(key, value);
                    break;
            }
        }

        public override void EnumerateTags<TProcessor>(ref TProcessor processor)
        {
            if (Id is not null)
            {
                processor.Process(new TagItem<string>(""IdTag"", Id, IdBytes));
            }

            if (Test is not null)
            {
                processor.Process(new TagItem<string>(""TestId"", Test, TestBytes));
            }

            if (Name is not null)
            {
                processor.Process(new TagItem<string>(""NameTag"", Name, NameBytes));
            }

            base.EnumerateTags(ref processor);
        }

        protected override void WriteAdditionalTags(System.Text.StringBuilder sb)
        {
            if (Id is not null)
            {
                sb.Append(""IdTag (tag):"")
                  .Append(Id)
                  .Append(',');
            }

            if (Test is not null)
            {
                sb.Append(""TestId (tag):"")
                  .Append(Test)
                  .Append(',');
            }

            if (Name is not null)
            {
                sb.Append(""NameTag (tag):"")
                  .Append(Name)
                  .Append(',');
            }

            base.WriteAdditionalTags(sb);
        }
    }
}
";
            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<TagListGenerator>(input);
            Assert.Equal(expected, output);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public void CanGenerateTagsListForReadOnlyMetric()
        {
            const string input = @"using Datadog.Trace.SourceGenerators;
namespace MyTests.TestListNameSpace
{
    public class TestList 
    { 
        [Metric(""IdMetric"")]
    	public double? Id { get; } = ""Some Value"";

        [Metric(""TestId"")]
    	public double? Test { get; set; }

        [Metric(""NameMetric"")]
    	public double? Name => ""Some Name"";
    }
}";
            const string expected = Constants.FileHeader + @"using Datadog.Trace.Processors;
using Datadog.Trace.Tagging;
using System;

namespace MyTests.TestListNameSpace
{
    partial class TestList
    {
        // IdBytes = MessagePack.Serialize(""IdMetric"");
        private static ReadOnlySpan<byte> IdBytes => new byte[] { 168, 73, 100, 77, 101, 116, 114, 105, 99 };
        // TestBytes = MessagePack.Serialize(""TestId"");
        private static ReadOnlySpan<byte> TestBytes => new byte[] { 166, 84, 101, 115, 116, 73, 100 };
        // NameBytes = MessagePack.Serialize(""NameMetric"");
        private static ReadOnlySpan<byte> NameBytes => new byte[] { 170, 78, 97, 109, 101, 77, 101, 116, 114, 105, 99 };

        public override double? GetMetric(string key)
        {
            return key switch
            {
                ""IdMetric"" => Id,
                ""TestId"" => Test,
                ""NameMetric"" => Name,
                _ => base.GetMetric(key),
            };
        }

        public override void SetMetric(string key, double? value)
        {
            switch(key)
            {
                case ""TestId"": 
                    Test = value;
                    break;
                case ""IdMetric"": 
                case ""NameMetric"": 
                    Logger.Value.Warning(""Attempted to set readonly metric {MetricName} on {TagType}. Ignoring."", key, nameof(TestList));
                    break;
                default: 
                    base.SetMetric(key, value);
                    break;
            }
        }

        public override void EnumerateMetrics<TProcessor>(ref TProcessor processor)
        {
            if (Id is not null)
            {
                processor.Process(new TagItem<double>(""IdMetric"", Id.Value, IdBytes));
            }

            if (Test is not null)
            {
                processor.Process(new TagItem<double>(""TestId"", Test.Value, TestBytes));
            }

            if (Name is not null)
            {
                processor.Process(new TagItem<double>(""NameMetric"", Name.Value, NameBytes));
            }

            base.EnumerateMetrics(ref processor);
        }

        protected override void WriteAdditionalMetrics(System.Text.StringBuilder sb)
        {
            if (Id is not null)
            {
                sb.Append(""IdMetric (metric):"")
                  .Append(Id.Value)
                  .Append(',');
            }

            if (Test is not null)
            {
                sb.Append(""TestId (metric):"")
                  .Append(Test.Value)
                  .Append(',');
            }

            if (Name is not null)
            {
                sb.Append(""NameMetric (metric):"")
                  .Append(Name.Value)
                  .Append(',');
            }

            base.WriteAdditionalMetrics(sb);
        }
    }
}
";
            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<TagListGenerator>(input);
            Assert.Equal(expected, output);
            Assert.Empty(diagnostics);
        }

        [Fact]
        public void CanNotGenerateTagsListWithTagThatContainsOrigin()
        {
            const string input = @"using Datadog.Trace.SourceGenerators;
namespace MyTests.TestListNameSpace
{
    public class TestList 
    { 
        [Tag(""TestId"")]
    	public string Id { get; set; }

        [Tag(""_dd.origin"")]
    	public string Origin { get; set; }
    }
}";
            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<TagListGenerator>(input);
            Assert.Contains(diagnostics, diag => diag.Id == InvalidUseOfOriginDiagnostic.Id);
        }

        [Fact]
        public void CanNotGenerateTagsListWithTagThatContainsLanguage()
        {
            const string input = @"using Datadog.Trace.SourceGenerators;
namespace MyTests.TestListNameSpace
{
    public class TestList 
    { 
        [Tag(""TestId"")]
    	public string Id { get; set; }

        [Tag(""language"")]
    	public string Language { get; set; }
    }
}";
            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<TagListGenerator>(input);
            Assert.Contains(diagnostics, diag => diag.Id == InvalidUseOfLanguageDiagnostic.Id);
        }

        [Theory]
        [InlineData(@"null")]
        [InlineData("\"\"")]
        public void CantUseAnEmptyTagName(string key)
        {
            var input = @"using Datadog.Trace.SourceGenerators;
namespace MyTests.TestListNameSpace
{
    public class TestList 
    { 
        [Tag(" + key + @")]
    	public string Id { get; set; }
    }
}";
            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<TagListGenerator>(input);
            Assert.Contains(diagnostics, diag => diag.Id == InvalidKeyDiagnostic.Id);
        }

        [Theory]
        [InlineData(@"null")]
        [InlineData("\"\"")]
        public void CantUseAnEmptyMetricName(string key)
        {
            var input = @"using Datadog.Trace.SourceGenerators;
namespace MyTests.TestListNameSpace
{
    public class TestList 
    { 
        [Metric(" + key + @")]
    	public double? Id { get; set; }
    }
}";
            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<TagListGenerator>(input);
            Assert.Contains(diagnostics, diag => diag.Id == InvalidKeyDiagnostic.Id);
        }

        [Fact]
        public void CantUseBothMetricNameAndTagName()
        {
            var input = @"using Datadog.Trace.SourceGenerators;
namespace MyTests.TestListNameSpace
{
    public class TestList 
    { 
        [Tag(""MyTagName"")]
        [Metric(""MyMetricName"")]
    	public string Id { get; set; }
    }
}";
            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<TagListGenerator>(input);
            Assert.Contains(diagnostics, diag => diag.Id == InvalidMetricPropertyReturnTypeDiagnostic.Id);
        }

        [Theory]
        [InlineData("double")]
        [InlineData("double?")]
        [InlineData("SomeOtherType")]
        public void CantUseWrongTypeForTagProperty(string returnType)
        {
            var input = @"using Datadog.Trace.SourceGenerators;
namespace MyTests.TestListNameSpace
{
    public class TestList 
    { 
        [Tag(""MyTagName"")]
    	public " + returnType + @" Id { get; set; }
    }
}";
            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<TagListGenerator>(input);
            Assert.Contains(diagnostics, diag => diag.Id == InvalidTagPropertyReturnTypeDiagnostic.Id);
        }

        [Theory]
        [InlineData("string")]
        [InlineData("double")]
        [InlineData("int")]
        [InlineData("int?")]
        [InlineData("SomeOtherType")]
        public void CantUseWrongTypeForMetricProperty(string returnType)
        {
            var input = @"using Datadog.Trace.SourceGenerators;
namespace MyTests.TestListNameSpace
{
    public class TestList 
    { 
        [Metric(""MyTagName"")]
    	public " + returnType + @" Id { get; set; }
    }
}";
            var (diagnostics, output) = TestHelpers.GetGeneratedOutput<TagListGenerator>(input);
            Assert.Contains(diagnostics, diag => diag.Id == InvalidMetricPropertyReturnTypeDiagnostic.Id);
        }
    }
}
