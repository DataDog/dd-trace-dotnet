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

namespace GenerateDocumentation
{
    public class DocumentationGenerator
    {
        private readonly AbsolutePath _spanModelRulesFilePath;
        private readonly AbsolutePath _docsDirectory;

        public DocumentationGenerator(
            AbsolutePath spanModelRulesFilePath,
            AbsolutePath docsDirectory)
        {
            _spanModelRulesFilePath = spanModelRulesFilePath;
            _docsDirectory = docsDirectory;

            if (!File.Exists(_spanModelRulesFilePath))
            {
                throw new Exception($"Definitions file {_spanModelRulesFilePath} does not exist. Exiting.");
            }
        }

        public void GenerateDocumentation()
        {
            var contents = File.ReadAllText(_spanModelRulesFilePath);
            if (contents == null)
            {
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("# Span Metadata");

            var reader = new StringReader(contents);
            var currentModel = new SpanModel();
            while (true)
            {
                string line = reader.ReadLine();

                if (line == null)
                {
                    if (currentModel.State == SpanModel.ModelState.Initialized
                        || currentModel.State == SpanModel.ModelState.AtLeastOneRequirementAdded)
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
                        line = line.Replace("``", ""); // Strip escaped names
                        functionStartIndex = line.IndexOf("let is");
                        if (functionStartIndex > -1)
                        {
                            functionStartIndex += 6;
                            functionEndIndex = line.IndexOf(":");
                            currentModel.SectionName = line.Substring(functionStartIndex, functionEndIndex - functionStartIndex).TrimEnd();
                            currentModel.State = SpanModel.ModelState.Initialized;
                        }
                        break;
                    case SpanModel.ModelState.Initialized:
                    case SpanModel.ModelState.AtLeastOneRequirementAdded:
                        // Finish the section
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            GenerateSectionMarkdown(sb, currentModel);
                            currentModel = new SpanModel();
                        }
                        // Add requirements
                        else
                        {
                            functionStartIndex = line.IndexOf("&&&");
                            if (functionStartIndex > -1)
                            {
                                functionStartIndex += 4;
                                var newRequirement = SpanModel.Requirement.GenerateRequirement(line.Substring(functionStartIndex));
                                currentModel.Requirements.Add(newRequirement);
                            }
                            else if (currentModel.State == SpanModel.ModelState.Initialized)
                            {
                                var newRequirement = SpanModel.Requirement.GenerateRequirement(line.Trim());
                                currentModel.Requirements.Add(newRequirement);
                            }

                            currentModel.State = SpanModel.ModelState.AtLeastOneRequirementAdded;
                        }

                        break;
                    default:
                        break;
                }
            }

            var spanModelFilePath = _docsDirectory / "span_metadata.md";
            File.WriteAllText(spanModelFilePath, sb.ToString());
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
                AtLeastOneRequirementAdded,
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
                
                private static string Capitalize(string input)
                {
                    // This is a really bad implementation but whatever
                    return input.Substring(0, 1).ToUpper() + input.Substring(1);
                }

                public static Requirement GenerateRequirement(string line)
                {
                    var parts = line.Split();
                    if (parts is null || parts.Length == 0)
                    {
                        return Unknown;
                    }

                    for (int i = 0; i < parts.Length; i++)
                    {
                        parts[i] = parts[i].Replace("`", "").Trim('"');
                    }
                    
                    return parts[0] switch
                    {
                        null => Unknown,
                        "isPresent" => new Requirement
                            {
                                Property = $"{Capitalize(parts[1])}",
                                RequiredValue = $"Yes",
                            },
                        "isPresentAndNonZero" => new Requirement
                            {
                                Property = $"{Capitalize(parts[1])}",
                                RequiredValue = $"Yes, _non-zero value_",
                            },
                        "matches" => new Requirement
                            {
                                Property = $"{Capitalize(parts[1])}",
                                RequiredValue = $"`{parts[2]}`",
                            },
                        "tagIsOptional" => new Requirement
                            {
                                Property = $"{parts[1]}",
                                PropertyType = PropertyType.Tag,
                                RequiredValue = "No",
                            },
                        "tagIsPresent" => new Requirement
                            {
                                Property = $"{parts[1]}",
                                PropertyType = PropertyType.Tag,
                                RequiredValue = "Yes",
                            },
                        "tagMatches" => new Requirement
                            {
                                Property = $"{parts[1]}",
                                PropertyType = PropertyType.Tag,
                                RequiredValue = $"`{parts[2]}`",
                            },
                        "metricIsPresent" => new Requirement
                            {
                                Property = $"{parts[1]}",
                                PropertyType = PropertyType.Metric,
                                RequiredValue = "Yes",
                            },
                        "metricMatches" => new Requirement
                            {
                                Property = $"{parts[1]}",
                                PropertyType = PropertyType.Metric,
                                RequiredValue = $"`{parts[2]}`",
                            },
                        _ => throw new Exception($"Requirement {parts[0]} not recognized"),
                    };
                }
            }
        }
    }
}
