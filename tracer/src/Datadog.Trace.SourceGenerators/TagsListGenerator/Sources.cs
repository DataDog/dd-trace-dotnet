// <copyright file="Sources.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Text;
using Datadog.Trace.SourceGenerators.Helpers;

namespace Datadog.Trace.SourceGenerators.TagsListGenerator
{
    internal static class Sources
    {
        public const string Attributes = Constants.FileHeader +
            @"namespace Datadog.Trace.SourceGenerators;

/// <summary>
/// Used to designate a property as corresponding to the provided
/// <see cref=""TagName""/>. Should only be used in ITags classes.
/// Used for source generation.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Property, AllowMultiple = false)]
internal sealed class TagAttribute : System.Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref=""TagAttribute""/> class.
    /// </summary>
    /// <param name=""tagName"">The name of the datadog tag the property corresponds to</param>
    public TagAttribute(string tagName) =>
        this.TagName = tagName;

    /// <summary>
    /// Gets the name of the datadog tag the property corresponds to
    /// </summary>
    public string TagName { get; }
}

/// <summary>
/// Used to designate a property as corresponding to the provided
/// <see cref=""MetricName""/>. Should only be used in ITags classes.
/// Used for source generation.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Property, AllowMultiple = false)]
internal sealed class MetricAttribute : System.Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref=""MetricAttribute""/> class.
    /// </summary>
    /// <param name=""metricName"">The name of the datadog metric the property corresponds to</param>
    public MetricAttribute(string metricName) =>
        this.MetricName = metricName;

    /// <summary>
    /// Gets the name of the datadog tag the property corresponds to
    /// </summary>
    public string MetricName { get; }
}
";

        public static string CreateTagsList(StringBuilder sb, TagListGenerator.TagList tagList)
        {
            sb.Append(Constants.FileHeader);
            sb.Append(@"using Datadog.Trace.Processors;
using Datadog.Trace.Tagging;
using System;

namespace ");
            sb.Append(tagList.Namespace)
              .Append(
                   @"
{
    partial class ")
              .Append(tagList.ClassName)
              .Append(
                   @"
    {");
            if (tagList.MetricProperties.HasValues())
            {
                foreach (var property in tagList.MetricProperties)
                {
                    var tagByteArray = string.Join(", ", MessagePackHelper.GetValueInRawMessagePackIEnumerable(property.TagValue));

                    sb.Append(
                           @"
        // ")
                      .Append(property.PropertyName)
                      .Append(@"Bytes = MessagePack.Serialize(""")
                      .Append(property.TagValue)
                      .Append(@""");");

                    sb.Append(
                            @"
        private static ReadOnlySpan<byte> ")
                        .Append(property.PropertyName)
                        .Append(@"Bytes => new byte[] { ")
                        .Append(tagByteArray)
                        .Append(@" };");
                }
            }

            if (tagList.TagProperties.HasValues())
            {
                foreach (var property in tagList.TagProperties)
                {
                    var tagByteArray = string.Join(", ", MessagePackHelper.GetValueInRawMessagePackIEnumerable(property.TagValue));

                    sb.Append(
                           @"
        // ")
                      .Append(property.PropertyName)
                      .Append(@"Bytes = MessagePack.Serialize(""")
                      .Append(property.TagValue)
                      .Append(@""");");

                    sb.Append(
                            @"
        private static ReadOnlySpan<byte> ")
                        .Append(property.PropertyName)
                        .Append(@"Bytes => new byte[] { ")
                        .Append(tagByteArray)
                        .Append(@" };");
                }

                sb.Append(
                    @"

        public override string? GetTag(string key)
        {
            return key switch
            {
                ");

                for (int i = 0; i < tagList.TagProperties.Length; i++)
                {
                    var property = tagList.TagProperties[i];
                    sb.Append('"')
                      .Append(property.TagValue)
                      .Append(@""" => ")
                      .Append(property.PropertyName)
                      .Append(
                           @",
                ");
                }

                sb.Append(
                    @"_ => base.GetTag(key),
            };
        }

        public override void SetTag(string key, string value)
        {
            switch(key)
            {
                ");

                for (int i = 0; i < tagList.TagProperties.Length; i++)
                {
                    var property = tagList.TagProperties[i];
                    if (property.IsReadOnly)
                    {
                        continue;
                    }

                    sb.Append(@"case """)
                      .Append(property.TagValue)
                      .Append(
                           @""": 
                    ")
                      .Append(property.PropertyName)
                      .Append(
                           @" = value;
                    break;
                ");
                }

                var haveReadOnlyTags = false;
                for (int i = 0; i < tagList.TagProperties.Length; i++)
                {
                    var property = tagList.TagProperties[i];
                    if (property.IsReadOnly)
                    {
                        haveReadOnlyTags = true;
                        sb.Append(@"case """)
                          .Append(property.TagValue)
                          .Append(
                               @""": 
                ");
                    }
                }

                if (haveReadOnlyTags)
                {
                    sb
                       .Append(@"    Logger.Value.Warning(""Attempted to set readonly tag {TagName} on {TagType}. Ignoring."", key, nameof(")
                       .Append(tagList.ClassName)
                       .Append(
                            @"));
                    break;
                ");
                }

                sb.Append(
                    @"default: 
                    base.SetTag(key, value);
                    break;
            }
        }

        public override void EnumerateTags<TProcessor>(ref TProcessor processor)
        {
            ");
                foreach (var property in tagList.TagProperties)
                {
                    sb.Append(@"if (")
                      .Append(property.PropertyName)
                      .Append(@" is not null)
            {
                processor.Process(new TagItem<string>(""")
                      .Append(property.TagValue)
                      .Append(@""", ")
                      .Append(property.PropertyName)
                      .Append(@", ")
                      .Append(property.PropertyName)
                      .Append(@"Bytes));
            }

            ");
                }

                sb.Append(
                    @"base.EnumerateTags(ref processor);
        }

        protected override void WriteAdditionalTags(System.Text.StringBuilder sb)
        {
            ");
                foreach (var property in tagList.TagProperties)
                {
                    sb.Append(@"if (")
                      .Append(property.PropertyName)
                      .Append(
                           @" is not null)
            {
                sb.Append(""")
                      .Append(property.TagValue)
                      .Append(@" (tag):"")
                  .Append(")
                      .Append(property.PropertyName)
                      .Append(
                           @")
                  .Append(',');
            }

            ");
                }

                sb.Append(@"base.WriteAdditionalTags(sb);
        }");
            }

            if (tagList.MetricProperties.HasValues())
            {
                if (tagList.TagProperties.IsDefaultOrEmpty)
                {
                    sb.AppendLine();
                }

                sb.Append(
                    @"
        public override double? GetMetric(string key)
        {
            return key switch
            {
                ");

                foreach (var property in tagList.MetricProperties)
                {
                    sb.Append('"')
                      .Append(property.TagValue)
                      .Append(@""" => ")
                      .Append(property.PropertyName)
                      .Append(
                               @",
                ");
                }

                sb.Append(
                    @"_ => base.GetMetric(key),
            };
        }

        public override void SetMetric(string key, double? value)
        {
            switch(key)
            {
                ");

                foreach (var property in tagList.MetricProperties)
                {
                    if (property.IsReadOnly)
                    {
                        continue;
                    }

                    sb.Append(@"case """)
                      .Append(property.TagValue)
                      .Append(
                           @""": 
                    ")
                      .Append(property.PropertyName)
                      .Append(
                           @" = value;
                    break;
                ");
                }

                var haveReadOnlyMetrics = false;
                for (int i = 0; i < tagList.MetricProperties.Length; i++)
                {
                    var property = tagList.MetricProperties[i];
                    if (property.IsReadOnly)
                    {
                        haveReadOnlyMetrics = true;
                        sb.Append(@"case """)
                          .Append(property.TagValue)
                          .Append(
                               @""": 
                ");
                    }
                }

                if (haveReadOnlyMetrics)
                {
                    sb
                       .Append(@"    Logger.Value.Warning(""Attempted to set readonly metric {MetricName} on {TagType}. Ignoring."", key, nameof(")
                       .Append(tagList.ClassName)
                       .Append(
                            @"));
                    break;
                ");
                }

                sb.Append(
                    @"default: 
                    base.SetMetric(key, value);
                    break;
            }
        }

        public override void EnumerateMetrics<TProcessor>(ref TProcessor processor)
        {
            ");
                foreach (var property in tagList.MetricProperties)
                {
                    sb.Append(@"if (")
                      .Append(property.PropertyName)
                      .Append(@" is not null)
            {
                processor.Process(new TagItem<double>(""")
                      .Append(property.TagValue)
                      .Append(@""", ")
                      .Append(property.PropertyName)
                      .Append(@".Value, ")
                      .Append(property.PropertyName)
                      .Append(@"Bytes));
            }

            ");
                }

                sb.Append(
                    @"base.EnumerateMetrics(ref processor);
        }

        protected override void WriteAdditionalMetrics(System.Text.StringBuilder sb)
        {
            ");
                foreach (var property in tagList.MetricProperties)
                {
                    sb.Append(@"if (")
                      .Append(property.PropertyName)
                      .Append(
                           @" is not null)
            {
                sb.Append(""")
                      .Append(property.TagValue)
                      .Append(@" (metric):"")
                  .Append(")
                      .Append(property.PropertyName)
                      .Append(
                           @".Value)
                  .Append(',');
            }

            ");
                }

                sb.Append(@"base.WriteAdditionalMetrics(sb);
        }");
            }

            sb.AppendLine(@"
    }
}");

            return sb.ToString();
        }
    }
}
