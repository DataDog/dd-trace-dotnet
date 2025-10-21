// <copyright file="ConfigKeyAliasesSwitcherGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Datadog.Trace.SourceGenerators.Configuration;
using Datadog.Trace.SourceGenerators.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

/// <summary>
/// Source generator that reads supported-configurations.json and generates a switch case
/// for configuration key matching with alias support.
/// </summary>
[Generator]
public class ConfigKeyAliasesSwitcherGenerator : IIncrementalGenerator
{
    private const string SupportedConfigurationsFileName = "supported-configurations.json";
    private const string MainKeyParamName = "mainKey";
    private const string ClassName = "ConfigKeyAliasesSwitcher";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Get the supported-configurations.json file and parse only the aliases section
        // We only track changes to the aliases section since that's what affects the generated code
        var additionalText = context.AdditionalTextsProvider
                                    .Where(static file => Path.GetFileName(file.Path).Equals(SupportedConfigurationsFileName, StringComparison.OrdinalIgnoreCase))
                                    .WithTrackingName(TrackingNames.ConfigurationKeysAdditionalText);

        var aliasSection = additionalText.Collect()
                                        .Select(static (files, ct) =>
                                        {
                                            if (files.Length == 0)
                                            {
                                                // No supported-configurations.json file found
                                                return new Result<string>(
                                                    string.Empty,
                                                    new EquatableArray<DiagnosticInfo>(
                                                    [
                                                        CreateDiagnosticInfo("DDSG0003", "Configuration file not found", $"The file '{SupportedConfigurationsFileName}' was not found. Make sure the supported-configurations.json file exists and is included as an AdditionalFile.", DiagnosticSeverity.Error)
                                                    ]));
                                            }

                                            // Extract from the first (and should be only) file
                                            return ExtractAliasesSection(files[0], ct);
                                        });

        var aliasesContent = aliasSection.Select(static (extractResult, ct) =>
                                          {
                                              if (extractResult.Errors.Count > 0)
                                              {
                                                  // Return the errors from extraction
                                                  return new Result<ConfigurationAliases>(null!, extractResult.Errors);
                                              }

                                              return ParseAliasesContent(extractResult.Value, ct);
                                          })
                                         .WithTrackingName(TrackingNames.ConfigurationKeysParseConfiguration);

        // Always generate source code, even when there are errors
        // This ensures compilation doesn't fail due to missing generated types
        context.RegisterSourceOutput(
            aliasesContent,
            static (spc, result) => Execute(spc, result));
    }

    private static void Execute(SourceProductionContext context, Result<ConfigurationAliases> result)
    {
        // Report any diagnostics first
        foreach (var diagnostic in result.Errors)
        {
            context.ReportDiagnostic(Diagnostic.Create(diagnostic.Descriptor, diagnostic.Location?.ToLocation()));
        }

        // Generate source code even if there are errors (use empty configuration as fallback)
        var configurationAliases = result.Value ?? new ConfigurationAliases(new Dictionary<string, string[]>());
        var compilationUnit = GenerateConfigurationKeyMatcher(configurationAliases);
        var generatedSource = compilationUnit.NormalizeWhitespace().ToFullString();
        context.AddSource($"{ClassName}.g.cs", SourceText.From(generatedSource, Encoding.UTF8));
    }

    private static Result<string> ExtractAliasesSection(AdditionalText file, CancellationToken cancellationToken)
    {
        try
        {
            var sourceText = file.GetText(cancellationToken);
            if (sourceText is null)
            {
                return new Result<string>(
                    string.Empty,
                    new EquatableArray<DiagnosticInfo>(
                    [
                        CreateDiagnosticInfo("DDSG0003", "Configuration file not found", $"The file '{file.Path}' could not be read. Make sure the supported-configurations.json file exists and is included as an AdditionalFile.", DiagnosticSeverity.Error)
                    ]));
            }

            var jsonContent = sourceText.ToString();

            // Extract only the aliases section from the JSON
            var aliasesSection = JsonReader.ExtractJsonObjectSection(jsonContent, "aliases");
            return new Result<string>(aliasesSection ?? string.Empty, default);
        }
        catch (Exception ex)
        {
            return new Result<string>(
                string.Empty,
                new EquatableArray<DiagnosticInfo>(
                [
                    CreateDiagnosticInfo("DDSG0004", "Configuration file read error", $"Failed to read configuration file '{file.Path}': {ex.Message}", DiagnosticSeverity.Error)
                ]));
        }
    }

    private static Result<ConfigurationAliases> ParseAliasesContent(string aliasesContent, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrEmpty(aliasesContent))
            {
                // Empty aliases section is valid - just return empty configuration
                return new Result<ConfigurationAliases>(new ConfigurationAliases(new Dictionary<string, string[]>()), default);
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Parse only the aliases section
            var aliases = JsonReader.ParseAliasesFromJson(aliasesContent);
            var configurationData = new ConfigurationAliases(aliases);

            return new Result<ConfigurationAliases>(configurationData, default);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new Result<ConfigurationAliases>(
                null!,
                new EquatableArray<DiagnosticInfo>(
                [
                    CreateDiagnosticInfo("DDSG0002", "Aliases parsing error", $"Failed to parse aliases section: {ex.Message}")
                ]));
        }
    }

    private static DiagnosticInfo CreateDiagnosticInfo(string id, string title, string message, DiagnosticSeverity severity = DiagnosticSeverity.Warning)
    {
        var descriptor = new DiagnosticDescriptor(
            id,
            title,
            message,
            "Configuration",
            severity,
            isEnabledByDefault: true);

        return new DiagnosticInfo(descriptor, Location.None);
    }

    private static CompilationUnitSyntax GenerateConfigurationKeyMatcher(ConfigurationAliases configurationAliases)
    {
        var getAliasesMethod = GenerateGetAliasesMethod(configurationAliases);

        var classDeclaration = ClassDeclaration(ClassName)
                              .WithModifiers(TokenList(Token(SyntaxKind.InternalKeyword), Token(SyntaxKind.StaticKeyword), Token(SyntaxKind.PartialKeyword)))
                              .WithLeadingTrivia(
                                   Comment("/// <summary>"),
                                   Comment("/// Generated configuration key matcher that handles main keys and aliases."),
                                   Comment("/// </summary>"))
                              .WithMembers(
                                   List<MemberDeclarationSyntax>(
                                   [
                                       getAliasesMethod
                                   ]));

        var namespaceDeclaration = FileScopedNamespaceDeclaration(
                QualifiedName(
                    QualifiedName(
                        IdentifierName("Datadog"),
                        IdentifierName("Trace")),
                    IdentifierName("Configuration")))
           .WithMembers(SingletonList<MemberDeclarationSyntax>(classDeclaration));

        return CompilationUnit()
              .WithLeadingTrivia(
                   Comment("// <auto-generated />"),
                   CarriageReturnLineFeed)
              .WithUsings(
                   SingletonList(
                       UsingDirective(IdentifierName("System"))))
              .WithMembers(SingletonList<MemberDeclarationSyntax>(namespaceDeclaration));
    }

    private static MethodDeclarationSyntax GenerateGetAliasesMethod(ConfigurationAliases configurationAliases)
    {
        var switchSections = new List<SwitchSectionSyntax>();

        // Add cases for keys that have aliases
        foreach (var alias in configurationAliases.Aliases.OrderBy(a => a.Key))
        {
            var mainKey = alias.Key;
            var aliasKeys = alias.Value;

            var arrayElements = aliasKeys
                               .OrderBy(a => a)
                               .Select(aliasKey => LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(aliasKey)))
                               .Cast<ExpressionSyntax>()
                               .ToArray();

            var arrayCreation = ArrayCreationExpression(
                    ArrayType(PredefinedType(Token(SyntaxKind.StringKeyword)))
                       .WithRankSpecifiers(SingletonList(ArrayRankSpecifier(SingletonSeparatedList<ExpressionSyntax>(OmittedArraySizeExpression())))))
               .WithInitializer(InitializerExpression(SyntaxKind.ArrayInitializerExpression, SeparatedList(arrayElements)));

            var switchSection = SwitchSection()
                               .WithLabels(
                                    SingletonList<SwitchLabelSyntax>(
                                        CaseSwitchLabel(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(mainKey)))))
                               .WithStatements(SingletonList<StatementSyntax>(ReturnStatement(arrayCreation)));
            switchSections.Add(switchSection);
        }

        // Add default case
        var defaultSection = SwitchSection()
                            .WithLabels(SingletonList<SwitchLabelSyntax>(DefaultSwitchLabel()))
                            .WithStatements(
                                 SingletonList<StatementSyntax>(
                                     ReturnStatement(
                                         InvocationExpression(
                                             MemberAccessExpression(
                                                 SyntaxKind.SimpleMemberAccessExpression,
                                                 IdentifierName("Array"),
                                                 GenericName("Empty")
                                                    .WithTypeArgumentList(
                                                         TypeArgumentList(
                                                             SingletonSeparatedList<TypeSyntax>(
                                                                 PredefinedType(Token(SyntaxKind.StringKeyword))))))))));
        switchSections.Add(defaultSection);

        var switchStatement = SwitchStatement(IdentifierName(MainKeyParamName))
           .WithSections(List(switchSections));

        return MethodDeclaration(
                   ArrayType(PredefinedType(Token(SyntaxKind.StringKeyword)))
                      .WithRankSpecifiers(SingletonList(ArrayRankSpecifier(SingletonSeparatedList<ExpressionSyntax>(OmittedArraySizeExpression())))),
                   "GetAliases")
              .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword)))
              .WithParameterList(
                   ParameterList(
                       SingletonSeparatedList(
                           Parameter(Identifier(MainKeyParamName))
                              .WithType(PredefinedType(Token(SyntaxKind.StringKeyword))))))
              .WithLeadingTrivia(
                   Comment("/// <summary>"),
                   Comment("/// Gets all aliases for the given configuration key."),
                   Comment("/// </summary>"),
                   Comment($"/// <param name=\"{MainKeyParamName}\">The configuration key.</param>"),
                   Comment("/// <returns>An array of aliases for the key, or empty array if no aliases exist.</returns>"))
              .WithBody(Block(switchStatement));
    }

    private sealed class ConfigurationAliases(Dictionary<string, string[]> aliases) : IEquatable<ConfigurationAliases>
    {
        public Dictionary<string, string[]> Aliases { get; } = aliases;

        public bool Equals(ConfigurationAliases? other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            // Proper content comparison for change detection
            if (Aliases.Count != other.Aliases.Count)
            {
                return false;
            }

            foreach (var kvp in Aliases)
            {
                if (!other.Aliases.TryGetValue(kvp.Key, out var otherAliases))
                {
                    return false;
                }

                if (kvp.Value.Length != otherAliases.Length)
                {
                    return false;
                }

                for (int i = 0; i < kvp.Value.Length; i++)
                {
                    if (kvp.Value[i] != otherAliases[i])
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public override bool Equals(object? obj)
        {
            return ReferenceEquals(this, obj) || (obj is ConfigurationAliases other && Equals(other));
        }

        public override int GetHashCode()
        {
            var hash = new System.HashCode();
            hash.Add(Aliases.Count);

            // Include content in hash for proper change detection
            foreach (var kvp in Aliases.OrderBy(x => x.Key))
            {
                hash.Add(kvp.Key);
                foreach (var alias in kvp.Value.OrderBy(x => x))
                {
                    hash.Add(alias);
                }
            }

            return hash.ToHashCode();
        }
    }
}
