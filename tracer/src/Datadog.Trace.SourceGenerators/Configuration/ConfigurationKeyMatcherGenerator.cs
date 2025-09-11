// <copyright file="ConfigurationKeyMatcherGenerator.cs" company="Datadog">
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
public class ConfigurationKeyMatcherGenerator : IIncrementalGenerator
{
    private const string SupportedConfigurationsFileName = "supported-configurations.json";
    private const string MainKeyParamName = "mainKey";
    private const string ClassName = "ConfigurationKeyMatcher";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Get the supported-configurations.json file and parse only the aliases section
        // We only track changes to the aliases section since that's what affects the generated code
        var additionalText = context.AdditionalTextsProvider
                                    .Where(static file => Path.GetFileName(file.Path).Equals(SupportedConfigurationsFileName, StringComparison.OrdinalIgnoreCase)).WithTrackingName(TrackingNames.ConfigurationKeysAdditionalText);

        var aliasSection = additionalText.Select(static (file, ct) => ExtractAliasesSection(file, ct));

        var aliasesContent = aliasSection.Select(static (aliasesContent, ct) => ParseAliasesContent(aliasesContent, ct))
                                         .WithTrackingName(TrackingNames.ConfigurationKeysParseConfiguration)
                                         .Where(static result => result is not null);

        // Generate source for valid configuration data
        var validConfigurationData = aliasesContent
                                    .Where(static result => result.Value is not null)
                                    .Select(static (result, _) => result.Value)
                                    .WithTrackingName(TrackingNames.ConfigurationKeyMatcherValidData);

        context.RegisterSourceOutput(
            validConfigurationData,
            static (spc, configData) => Execute(spc, configData));

        // Report diagnostics for any parsing errors
        context.ReportDiagnostics(
            aliasesContent
               .Where(static result => result.Errors.Count > 0)
               .SelectMany(static (result, _) => result.Errors)
               .WithTrackingName(TrackingNames.ConfigurationKeyMatcherDiagnostics));
    }

    private static void Execute(SourceProductionContext context, ConfigurationAliases configurationAliases)
    {
        var compilationUnit = GenerateConfigurationKeyMatcher(configurationAliases);
        var generatedSource = compilationUnit.NormalizeWhitespace().ToFullString();
        context.AddSource($"{ClassName}.g.cs", SourceText.From(generatedSource, Encoding.UTF8));
    }

    private static string ExtractAliasesSection(AdditionalText file, CancellationToken cancellationToken)
    {
        try
        {
            var sourceText = file.GetText(cancellationToken);
            if (sourceText is null)
            {
                return string.Empty;
            }

            var jsonContent = sourceText.ToString();

            // Extract only the aliases section from the JSON
            var aliasesSection = JsonReader.ExtractJsonObjectSection(jsonContent, "aliases");
            return aliasesSection ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
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

    private static DiagnosticInfo CreateDiagnosticInfo(string id, string title, string message)
    {
        var descriptor = new DiagnosticDescriptor(
            id,
            title,
            message,
            "Configuration",
            DiagnosticSeverity.Warning,
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
