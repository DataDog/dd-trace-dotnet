// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nuke.Common.IO;

namespace GenerateSpanDocumentation
{
    public class SpanDocumentationGenerator
    {
        private const string HeaderConst =
@"This file is intended for development purposes only. The markdown is generated from assertions authored in files /tracer/test/Datadog.Trace.TestHelpers/SpanMetadata*Rules.cs and the assertions are actively tested in the tracing integration tests.

The Integration Name (used for configuring individual integrations) of each span corresponds to the markdown header, with the following exceptions:
- The `AspNetCoreMvc` span has the Integration Name `AspNetCore`";
        private readonly AbsolutePath _spanModelRulesFilePath;
        private readonly AbsolutePath _outputFilePath;

        readonly Regex methodNameRegex = new("^Is([a-zA-Z0-9]+)V[0-9]+$");

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

            var sb = new StringBuilder();
            sb.AppendLine("# Span Metadata");
            sb.AppendLine(HeaderConst);

            // parse C# into an AST
            var tree = CSharpSyntaxTree.ParseText(contents);
            var root = tree.GetCompilationUnitRoot();

            // iterate on all methods declared in the file
            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var method in methods)
            {
                // get the name of section (in the markdown, usually the integration) from the name of the method
                var match = methodNameRegex.Match(method.Identifier.ValueText);
                if (!match.Success)
                {
                    Console.WriteLine($"Ignoring method {method.Identifier.ValueText}");
                    continue;
                }

                var model = new SpanModel { SectionName = match.Groups[1].Value };

                // now get all the method invocations in the body of that method
                // (we get a lot more than we need at this step, so we filter in the switch)
                var calls = method.DescendantNodes().OfType<InvocationExpressionSyntax>();
                foreach (var call in calls)
                {
                    // We need the InvocationExpressionSyntax to access the arguments,
                    // and the MemberAccessExpressionSyntax to access the name of the method
                    if (call.Expression is not MemberAccessExpressionSyntax memberAccess) { continue; }

                    var propertyType = memberAccess.Name.ToString();
                    switch (propertyType)
                    {
                        case "Properties":
                        case "Tags":
                        case "AdditionalTags":
                        case "Metrics":
                            model.ParseRequirements(call.ArgumentList, Enum.Parse<SpanModel.PropertyType>(propertyType));
                            break;
                        case "WithIntegrationName":
                            model.IntegrationName = call.ArgumentList.Arguments[0].ToString().Trim('"');
                            break;
                        case "WithMarkdownSection":
                            model.SectionName = call.ArgumentList.Arguments[0].ToString().Trim('"');
                            break;
                        default:
                            // we fall here for all the "Matches", "isOptional", etc. that we handle in SpanModel.ParseRequirements
                            break;
                    }
                }

                GenerateSectionMarkdown(sb, model);
            }
            File.WriteAllText(_outputFilePath, sb.ToString());
        }

        private static void GenerateSectionMarkdown(StringBuilder sb, SpanModel model)
        {
            sb.AppendLine($"## {model.SectionName}");

            if (model.IntegrationName is not null)
            {
                sb.AppendLine($"> ⚠️ Note: This span is controlled by integration name `{model.IntegrationName}`");
            }

            // Add span properties first
            bool spanHeaderAdded = false;
            foreach (var requirement in model.Requirements
                         .Where(r => r.PropertyType == SpanModel.PropertyType.Properties)
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
                         .Where(r => r.PropertyType == SpanModel.PropertyType.Tags)
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

            // Add AdditionalTags next
            bool additionalTagsHeaderAdded = false;
            foreach (var requirement in model.Requirements
                                        .Where(r => r.PropertyType == SpanModel.PropertyType.AdditionalTags)
                                        .OrderBy(r => r.Property))
            {
                if (!additionalTagsHeaderAdded)
                {
                    sb.AppendLine("### AdditionalTags");
                    sb.AppendLine("Source | Operation | Required |");
                    sb.AppendLine("---------|-----------|----------------|");
                    additionalTagsHeaderAdded = true;
                }

                sb.AppendLine(requirement.ToString());
            }

            // Add Metrics next
            bool metricsHeaderAdded = false;
            foreach (var requirement in model.Requirements
                         .Where(r => r.PropertyType == SpanModel.PropertyType.Metrics)
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
            public string SectionName;
            public string IntegrationName;

            public List<Requirement> Requirements = new();

            public enum PropertyType
            {
                Properties,
                Tags,
                AdditionalTags,
                Metrics
            }

            public record Requirement
            {
                public PropertyType PropertyType { get; init; }
                public string Property { get; init; }
                public string OperationName { get; init; }
                public string RequiredValue { get; init; }

                public override string ToString()
                {
                    return OperationName != null
                               ? $"{Property} | {OperationName} | {RequiredValue}"
                               : $"{Property} | {RequiredValue}";
                }
            }

            public void ParseRequirements(ArgumentListSyntax arg, PropertyType propertyType)
            {
                var requirements = arg.DescendantNodes().OfType<InvocationExpressionSyntax>();
                foreach (var requirement in requirements)
                {
                    if (requirement.Expression is not MemberAccessExpressionSyntax memberAccess) { continue; }

                    var parameters = requirement.ArgumentList.Arguments.Select(x => x.ToString().Trim('"')).ToArray();

                    string property = parameters[0], operationName = null, requiredValue;
                    switch (memberAccess.Name.Identifier.Value)
                    {
                        case "Matches":
                            requiredValue = $"`{parameters[1]}`";
                            break;
                        case "IfPresentMatches":
                            requiredValue = $"Optional: `{parameters[1]}`";
                            break;
                        case "MatchesOneOf":
                            requiredValue = string.Join("; ", parameters.Skip(1).Select(s => $"`{s}`"));
                            break;
                        case "IfPresentMatchesOneOf":
                            requiredValue = "Optional: " + string.Join("; ", parameters.Skip(1).Select(s => $"`{s}`"));
                            break;
                        case "IsOptional": // only for tags
                            requiredValue = "No";
                            break;
                        case "IsPresent": // only for tags
                            requiredValue = "Yes";
                            break;
                        case "PassesThroughSource": // only for additional tags
                            operationName = "PassThru";
                            requiredValue = "No";
                            break;
                        default:
                            throw new Exception($"Invalid requirement:{memberAccess.Name.Identifier.Value}");
                    }

                    Requirements.Add(new Requirement { PropertyType = propertyType, Property = property, OperationName = operationName, RequiredValue = requiredValue });
                }
            }
        }
    }
}
