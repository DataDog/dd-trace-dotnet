// <copyright file="IncrementalGeneratorBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Helpers
{
    /// <summary>
    /// Base class
    /// </summary>
    public abstract class IncrementalGeneratorBase : IIncrementalGenerator, IAdditionalFileCodeGenerator
    {
        /// <summary>
        /// Gets or sets set additional file writer function
        /// </summary>
        public Action<string, string> WriteAdditionalFile { get; set; } = DefaultWriteAdditionalFile;

        /// <summary> Init </summary>
        /// <param name="context"> Ctx </param>
        public abstract void Initialize(IncrementalGeneratorInitializationContext context);

        internal static string GetTfm(GeneratorAttributeSyntaxContext context, CancellationToken ct)
        {
            var tfm = "ERROR";

            INamedTypeSymbol? classSymbol = context.TargetSymbol as INamedTypeSymbol;
            if (classSymbol is null)
            {
                return tfm;
            }

            ct.ThrowIfCancellationRequested();

            foreach (AttributeData attribute in classSymbol.GetAttributes())
            {
                if (attribute.AttributeClass is null) { continue; }
                if (attribute.AttributeClass.Name == "TargetFrameworkMonikerAttribute"
                  && attribute.ConstructorArguments.Length == 1)
                {
                    tfm = attribute.ConstructorArguments[0].Value?.ToString() ?? "ERROR";
                    break;
                }
            }

            return tfm;
        }

        internal static IncrementalValuesProvider<string> RegisterPlaceholder(IncrementalGeneratorInitializationContext context)
        {
            // Get path of the compiling project using the placeholder additional file
            var placeholderFile = context.AdditionalTextsProvider
                .Where(a => a.Path.EndsWith("placeholder.json"))
                .Select((a, c) => Path.GetDirectoryName(a.Path))
                .WithTrackingName(TrackingNames.Placeholder);
            return placeholderFile;
        }

        internal static IncrementalValuesProvider<string> RegisterTfm(IncrementalGeneratorInitializationContext context)
        {
            // Get the TFM from the TargetFrameworkMonikerAttribute
            var tfm = context.SyntaxProvider
                        .ForAttributeWithMetadataName(
                            "Datadog.Trace.Telemetry.TargetFrameworkMonikerAttribute",
                            predicate: (node, _) => node is ClassDeclarationSyntax,
                            transform: GetTfm)
                        .Where(static m => m is not null)
                        .WithTrackingName(TrackingNames.Tfm);
            return tfm;
        }

        private static void DefaultWriteAdditionalFile(string filePath, string source)
        {
            var destFolder = Path.GetDirectoryName(filePath);
            _ = Directory.CreateDirectory(destFolder);
            File.WriteAllText(filePath, source);
        }
    }
}
