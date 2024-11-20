using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.IO;

namespace GeneratePackageVersions
{
    public class InstrumentationAnalyzer
    {
        public class InstrumentationInfo
        {
            public string IntegrationName { get; set; }
            public string AssemblyName { get; set; }
            public string MinimumVersion { get; set; }
            public string MaximumVersion { get; set; }
            public string TypeName { get; set; }
            public string MethodName { get; set; }
        }

        public static async Task<List<InstrumentationInfo>> AnalyzeSourceDirectory(string sourceDirectory)
        {
            var instrumentationInfos = new List<InstrumentationInfo>();

            // Get all .cs files, excluding generated code and obj/bin folders
            var sourceFiles = Directory.GetFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories)
                                     .Where(f => !f.Contains("\\obj\\") &&
                                               !f.Contains("\\bin\\") &&
                                               !f.Contains("\\Generated\\"));

            foreach (var sourceFile in sourceFiles)
            {
                var sourceText = await File.ReadAllTextAsync(sourceFile);
                var tree = CSharpSyntaxTree.ParseText(sourceText);
                var root = await tree.GetRootAsync();

                // Find all attribute lists
                var attributeLists = root.DescendantNodes()
                    .OfType<AttributeListSyntax>();

                foreach (var attributeList in attributeLists)
                {
                    foreach (var attribute in attributeList.Attributes)
                    {
                        if (attribute.Name.ToString() == "InstrumentMethod")
                        {
                            var info = ExtractInstrumentationInfo(attribute);
                            if (info != null)
                            {
                                instrumentationInfos.Add(info);
                            }
                        }
                    }
                }
            }

            return instrumentationInfos;
        }

        private static InstrumentationInfo ExtractInstrumentationInfo(AttributeSyntax attribute)
        {
            var info = new InstrumentationInfo();

            // Handle both named arguments and array expressions
            foreach (var argument in attribute.ArgumentList?.Arguments ?? Enumerable.Empty<AttributeArgumentSyntax>())
            {
                if (argument.NameEquals != null)
                {
                    var name = argument.NameEquals.Name.ToString();
                    var value = ExtractArgumentValue(argument.Expression);

                    switch (name)
                    {
                        case "AssemblyName":
                            info.AssemblyName = value;
                            break;
                        case "AssemblyNames" when value != null:
                            // Handle array of assembly names
                            info.AssemblyName = value;
                            break;
                        case "TypeName":
                            info.TypeName = value;
                            break;
                        case "MethodName":
                            info.MethodName = value;
                            break;
                        case "MinimumVersion":
                            info.MinimumVersion = value;
                            break;
                        case "MaximumVersion":
                            info.MaximumVersion = value;
                            break;
                        case "IntegrationName":
                            info.IntegrationName = value;
                            break;
                    }
                }
            }

            return info.IntegrationName != null ? info : null;
        }

        private static string ExtractArgumentValue(ExpressionSyntax expression)
        {
            switch (expression)
            {
                case LiteralExpressionSyntax literal:
                    return literal.Token.ValueText;

                case ArrayCreationExpressionSyntax arrayCreation:
                    var values = arrayCreation.Initializer?.Expressions
                        .OfType<LiteralExpressionSyntax>()
                        .Select(l => l.Token.ValueText);
                    return values?.FirstOrDefault();

                case MemberAccessExpressionSyntax memberAccess:
                    return memberAccess.ToString();

                default:
                    return null;
            }
        }
    }
}
