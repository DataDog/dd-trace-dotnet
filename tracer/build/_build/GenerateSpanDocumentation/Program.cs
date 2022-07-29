// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Nuke.Common.IO;

namespace GenerateSpanDocumentation
{
    public class SpanDocumentationGenerator
    {
        private const string HeaderConst =
@"This file is intended for development purposes only. The markdown is generated from assertions authored [here](/tracer/test/Datadog.Trace.TestHelpers/SpanMetadataRules.cs) and the assertions are actively tested in the tracing integration tests.";
        private readonly AbsolutePath _spanModelRulesFilePath;
        private readonly AbsolutePath _outputFilePath;

        public SpanDocumentationGenerator(
            AbsolutePath spanModelRulesFilePath,
            AbsolutePath outputFilePath)
        {
            _spanModelRulesFilePath = spanModelRulesFilePath;
            _outputFilePath = outputFilePath;

            if (!File.Exists(_spanModelRulesFilePath))
            {
                throw new Exception($"Definitions file {_spanModelRulesFilePath} does not exist. Exiting.");
            }
        }

        public void Run()
        {
            var contents = File.ReadAllText(_spanModelRulesFilePath);
            if (contents == null)
            {
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("# Span Metadata");
            sb.AppendLine(HeaderConst);

            var reader = new StringReader(contents);
            var currentModel = new SpanModel();
            while (true)
            {
                string line = reader.ReadLine();

                if (line == null)
                {
                    if (currentModel.State == SpanModel.ModelState.Initialized)
                    {
                        GenerateSectionMarkdown(sb, currentModel);
                    }

                    break;
                }

                int functionStartIndex;
                int functionEndIndex;

                switch (currentModel.State)
                {
                    case SpanModel.ModelState.Missing:
                        functionStartIndex = line.IndexOf("public static Result Is");
                        if (functionStartIndex > -1)
                        {
                            functionStartIndex += 23;
                            functionEndIndex = line.IndexOf("(", functionStartIndex);
                            currentModel.SectionName = line.Substring(functionStartIndex, functionEndIndex - functionStartIndex).TrimEnd();
                            currentModel.State = SpanModel.ModelState.Initialized;
                        }
                        break;
                    case SpanModel.ModelState.Initialized:
                    case SpanModel.ModelState.ParsingProperties:
                    case SpanModel.ModelState.ParsingTags:
                    case SpanModel.ModelState.ParsingMetrics:
                        var trimmedLine = line.Trim();

                        // Finish the section
                        if (string.IsNullOrWhiteSpace(trimmedLine)
                            || trimmedLine.StartsWith("}"))
                        {
                            GenerateSectionMarkdown(sb, currentModel);
                            currentModel = new SpanModel();
                        }
                        else if (trimmedLine.StartsWith("//"))
                        {
                            // Do nothing
                        }
                        // Add requirements
                        else
                        {
                            bool processRequirement = true;
                            functionStartIndex = line.IndexOf("Properties(");
                            if (functionStartIndex > -1)
                            {
                                processRequirement = false;
                                currentModel.State = SpanModel.ModelState.ParsingProperties;
                            }

                            functionStartIndex = line.IndexOf("Tags(");
                            if (functionStartIndex > -1)
                            {
                                processRequirement = false;
                                currentModel.State = SpanModel.ModelState.ParsingTags;
                            }

                            functionStartIndex = line.IndexOf("Metrics(");
                            if (functionStartIndex > -1)
                            {
                                processRequirement = false;
                                currentModel.State = SpanModel.ModelState.ParsingMetrics;
                            }

                            if (processRequirement)
                            {
                                functionStartIndex = line.IndexOf(".");
                                functionStartIndex += 1;
                                var newRequirement = SpanModel.Requirement.GenerateRequirement(line.Substring(functionStartIndex), currentModel.State);
                                currentModel.Requirements.Add(newRequirement);
                            }
                        }

                        break;
                    default:
                        break;
                }
            }

            File.WriteAllText(_outputFilePath, sb.ToString());
        }

        private static void GenerateSectionMarkdown(StringBuilder sb, SpanModel model)
        {
            sb.AppendLine($"## {model.SectionName}");

            // Add span properties first
            bool spanHeaderAdded = false;
            foreach (var requirement in model.Requirements
                                        .Where(r => r.PropertyType == SpanModel.PropertyType.Span)
                                        .OrderBy(r => r.Property))
            {
                if (!spanHeaderAdded)
                {
                    sb.AppendLine("### Span properties");
                    sb.AppendLine("Name | Required |");
                    sb.AppendLine("---------|----------------|");
                    spanHeaderAdded = true;
                }

                sb.AppendLine(requirement.ToString());
            }

            // Add Tags next
            bool tagsHeaderAdded = false;
            foreach (var requirement in model.Requirements
                                        .Where(r => r.PropertyType == SpanModel.PropertyType.Tag)
                                        .OrderBy(r => r.Property))
            {
                if (!tagsHeaderAdded)
                {
                    sb.AppendLine("### Tags");
                    sb.AppendLine("Name | Required |");
                    sb.AppendLine("---------|----------------|");
                    tagsHeaderAdded = true;
                }

                sb.AppendLine(requirement.ToString());
            }

            // Add Metrics next
            bool metricsHeaderAdded = false;
            foreach (var requirement in model.Requirements
                                        .Where(r => r.PropertyType == SpanModel.PropertyType.Metric)
                                        .OrderBy(r => r.Property))
            {
                if (!metricsHeaderAdded)
                {
                    sb.AppendLine("### Metrics");
                    sb.AppendLine("Name | Required |");
                    sb.AppendLine("---------|----------------|");
                    metricsHeaderAdded = true;
                }

                sb.AppendLine(requirement.ToString());
            }

            sb.AppendLine();
        }

        public class SpanModel
        {
            public ModelState State = SpanModel.ModelState.Missing;
            public string SectionName;

            public List<Requirement> Requirements = new List<Requirement>();

            public enum ModelState
            {
                Missing,
                Initialized,
                ParsingProperties,
                ParsingTags,
                ParsingMetrics,
            }

            public enum PropertyType
            {
                Span,
                Tag,
                Metric,
            }

            public record Requirement
            {
                private static readonly Requirement Unknown = new Requirement
                {
                    Property = "unknown",
                    PropertyType = PropertyType.Span,
                    RequiredValue = "unknown",
                };

                public string Property { get; init; }
                public PropertyType PropertyType { get; init; }
                public string RequiredValue { get; init; }

                public override string ToString()
                    => $"{Property} | {RequiredValue}";

                public static Requirement GenerateRequirement(string line, ModelState state)
                {
                    var parts = line.Replace(";", "")
                                    .Replace(",", "")
                                    .Replace("(", " ")
                                    .Replace(")", "")
                                    .Split(" ", StringSplitOptions.RemoveEmptyEntries);
                    if (parts is null || parts.Length == 0)
                    {
                        return Unknown;
                    }

                    for (int i = 0; i < parts.Length; i++)
                    {
                        parts[i] = parts[i].Replace("`", "").Trim('"');
                    }

                    var propertyType = state switch
                    {
                        ModelState.ParsingProperties => PropertyType.Span,
                        ModelState.ParsingTags => PropertyType.Tag,
                        ModelState.ParsingMetrics => PropertyType.Metric,
                        _ => throw new ArgumentException()
                    };

                    return (state, parts[0]) switch
                    {
                        (_, "Matches") => new Requirement
                            {
                                Property = $"{parts[1]}",
                                PropertyType = propertyType,
                                RequiredValue = $"`{parts[2]}`",
                            },
                        (_, "MatchesOneOf") => new Requirement
                            {
                                Property = $"{parts[1]}",
                                PropertyType = propertyType,
                                RequiredValue = string.Join("; ", parts.Skip(2).Select(s => $"`{s}`")),
                            },
                        (ModelState.ParsingTags, "IsOptional") => new Requirement
                            {
                                Property = $"{parts[1]}",
                                PropertyType = propertyType,
                                RequiredValue = "No",
                            },
                        (ModelState.ParsingTags, "IsPresent") => new Requirement
                            {
                                Property = $"{parts[1]}",
                                PropertyType = propertyType,
                                RequiredValue = "Yes",
                            },
                        (_, _) => throw new Exception($"Invalid requirement. RequirementName:{parts[0]}, State:{state}"),
                    };
                }
            }
        }
    }
}
