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
            sb.AppendLine("# Span Model");

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

            var spanModelFilePath = _docsDirectory / "SpanModel.md";
            File.WriteAllText(spanModelFilePath, sb.ToString());
        }

        private static void GenerateSectionMarkdown(StringBuilder sb, SpanModel model)
        {
            sb.AppendLine($"## {model.SectionName} Metadata");
            sb.AppendLine("Property | Required Value |");
            sb.AppendLine("---------|----------------|");

            // Add span properties first
            foreach (var requirement in model.Requirements
                                        .Where(r => !r.Property.StartsWith("Tags[") && !r.Property.StartsWith("Metrics["))
                                        .OrderBy(r => r.Property))
            {
                sb.AppendLine(requirement.ToMarkdownString());
            }

            // Add Tags next
            foreach (var requirement in model.Requirements
                                        .Where(r => r.Property.StartsWith("Tags["))
                                        .OrderBy(r => r.Property))
            {
                sb.AppendLine(requirement.ToMarkdownString());
            }

            // Add Metrics next
            foreach (var requirement in model.Requirements
                                        .Where(r => r.Property.StartsWith("Metrics["))
                                        .OrderBy(r => r.Property))
            {
                sb.AppendLine(requirement.ToMarkdownString());
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

            public record Requirement
            {
                private static readonly Requirement Unknown = new Requirement
                {
                    Property = "unknown",
                    RequiredValue = "unknown"
                };

                public string Property { get; init; }
                public string RequiredValue { get; init; }

                public string ToMarkdownString()
                    => $"{Property} | {RequiredValue} |";
                
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
                        parts[i] = parts[i].Replace("`", "");
                    }
                    
                    return parts[0] switch
                    {
                        null => Unknown,
                        "isPresent" => new Requirement
                            {
                                Property = $"{Capitalize(parts[1])}",
                                RequiredValue = "_non-null value_",
                            },
                        "isPresentAndNonZero" => new Requirement
                            {
                                Property = $"{Capitalize(parts[1])}",
                                RequiredValue = "_non-zero value_",
                            },
                        "matches" => new Requirement
                            {
                                Property = $"{Capitalize(parts[1])}",
                                RequiredValue = $"`{parts[2]}`",
                            },
                        "tagIsPresent" => new Requirement
                            {
                                Property = $"Tags[{parts[1]}]",
                                RequiredValue = "_non-null value_",
                            },
                        "tagMatches" => new Requirement
                            {
                                Property = $"Tags[{parts[1]}]",
                                RequiredValue = $"`{parts[2]}`",
                            },
                        "metricIsPresent" => new Requirement
                            {
                                Property = $"Metrics[{parts[1]}]",
                                RequiredValue = "_non-null value_",
                            },
                        "metricMatches" => new Requirement
                            {
                                Property = $"Metrics[{parts[1]}]",
                                RequiredValue = $"`{parts[2]}`",
                            },
                        _ => throw new Exception($"Requirement {parts[0]} not recognized"),
                    };
                }
            }
        }
    }
}