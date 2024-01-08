// <copyright file="LogAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// Based on the Serilog analyzer in https://github.com/Suchiman/SerilogAnalyzer/blob/master/SerilogAnalyzer/SerilogAnalyzer/DiagnosticAnalyzer.cs

#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Datadog.Trace.Tools.Analyzers.Helpers;
using Datadog.Trace.Tools.Analyzers.LogAnalyzer.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Datadog.Trace.Tools.Analyzers.LogAnalyzer;

/// <summary>
/// An analyzer that attempts to look for common incorrect patterns in usages of Datadog.Trace.Logging.IDatadogLogger
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class LogAnalyzer : DiagnosticAnalyzer
{
    private const string DatadogLoggerType = "Datadog.Trace.Logging.IDatadogLogger";
    private const string DatadogLoggingType = "Datadog.Trace.Logging.DatadogLogging";
    private const string SerilogLoggerType = "Serilog.ILogger";
    private const string VendoredSerilogLoggerType = "Datadog.Trace.Vendors.Serilog.ILogger";
    private const string GetLoggerFor = "GetLoggerFor";
    private const string MessageTemplateName = "messageTemplate";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Diagnostics.ExceptionRule, Diagnostics.TemplateRule, Diagnostics.PropertyBindingRule, Diagnostics.ConstantMessageTemplateRule, Diagnostics.UniquePropertyNameRule, Diagnostics.PascalPropertyNameRule, Diagnostics.DestructureAnonymousObjectsRule, Diagnostics.UseCorrectContextualLoggerRule, Diagnostics.UseDatadogLoggerRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        // Ensure we analyze generated code too
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeSymbol, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeSymbol(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var info = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        var method = info.Symbol as IMethodSymbol;
        if (method == null)
        {
            return;
        }

        // check if GetLoggerFor<T> / GetLoggerFor(typeof(T)) calls use the containing type as T
        string? containingTypeName = null;
        if (method.Name == GetLoggerFor
         && method.ReturnType.ToString() == DatadogLoggerType
         && (containingTypeName = method.ContainingType.ToString()) == DatadogLoggingType)
        {
            CheckForContextCorrectness(ref context, invocation, method);
        }

        // is it an IDatadogLogger logging method?
        if (method.Name is not "Debug" and not "Information" and not "Warning" and not "Error")
        {
            return;
        }

        // is it an IDatadogLogger logger _instance_?
        containingTypeName ??= method.ContainingType.ToString();
        if (containingTypeName != DatadogLoggerType)
        {
            // is it a Serilog logger _instance_ (shouldn't be using these outside of vendored code)
            if (!context.IsGeneratedCode && containingTypeName is SerilogLoggerType or VendoredSerilogLoggerType)
            {
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.UseDatadogLoggerRule, invocation.GetLocation(), containingTypeName));
            }

            return;
        }

        // check for errors in the MessageTemplate
        var arguments = default(List<SourceArgument>);
        var properties = new List<PropertyToken>();
        var hasErrors = false;
        var literalSpan = default(TextSpan);
        var exactPositions = true;
        var stringText = default(string);
        var invocationArguments = invocation.ArgumentList.Arguments;
        foreach (var argument in invocationArguments)
        {
            var parameter = RoslynHelper.DetermineParameter(argument, context.SemanticModel, true, context.CancellationToken);
            if (parameter?.Name == MessageTemplateName)
            {
                string messageTemplate;

                // is it a simple string literal?
                if (argument.Expression is LiteralExpressionSyntax literal)
                {
                    stringText = literal.Token.Text;
                    exactPositions = true;

                    messageTemplate = literal.Token.ValueText;
                }
                else
                {
                    // can we at least get a computed constant value for it?
                    var constantValue = context.SemanticModel.GetConstantValue(argument.Expression, context.CancellationToken);
                    if (!constantValue.HasValue || !(constantValue.Value is string constString))
                    {
                        if (context.SemanticModel.GetSymbolInfo(argument.Expression, context.CancellationToken).Symbol is IFieldSymbol { Name: "Empty" } field
                         && field.Type.Equals(context.Compilation.GetSpecialType(SpecialType.System_String), SymbolEqualityComparer.Default))
                        {
                            constString = string.Empty;
                        }
                        else
                        {
                            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.ConstantMessageTemplateRule, argument.Expression.GetLocation(), argument.Expression.ToString()));
                            // if we don't have a constant string, just stop
                            break;
                        }
                    }

                    // we can't map positions back from the computed string into the real positions
                    exactPositions = false;
                    messageTemplate = constString;
                }

                literalSpan = argument.Expression.GetLocation().SourceSpan;

                var messageTemplateDiagnostics = AnalyzingMessageTemplateParser.Analyze(messageTemplate);
                foreach (var templateDiagnostic in messageTemplateDiagnostics)
                {
                    if (templateDiagnostic is PropertyToken property)
                    {
                        properties.Add(property);
                        continue;
                    }

                    if (templateDiagnostic is MessageTemplateDiagnostic diagnostic)
                    {
                        hasErrors = true;
                        ReportDiagnostic(ref context, ref literalSpan, stringText, exactPositions, Diagnostics.TemplateRule, diagnostic);
                    }
                }

                var messageTemplateArgumentIndex = invocationArguments.IndexOf(argument);

                // crude handling case where we pass an object[] as the single extra argument
                var nextParameterIndex = messageTemplateArgumentIndex + 1;
                if ((invocationArguments.Count == nextParameterIndex + 1)
                    && method.Parameters.Length > nextParameterIndex
                    && (method.Parameters[nextParameterIndex].Type.ToString() == "object[]"
                        || method.Parameters[nextParameterIndex].Type.ToString() == "object?[]"))
                {
                    // we're in the object[] version of the log message,
                    if (invocationArguments[nextParameterIndex].Expression is ArrayCreationExpressionSyntax { Initializer: { } initializer })
                    {
                        // The object[] is being created inline, e.g. new object[] {"arg1", "arg2"}
                        // so we fudge the analysis to treat these as individual arguments instead
                        arguments = initializer.Expressions.Select(
                            x =>
                            {
                                var location = x.GetLocation().SourceSpan;
                                return new SourceArgument(x, location.Start, location.Length);
                            }).ToList();
                        break;
                    }
                    else
                    {
                        // The object[] comes from somewhere else, which is hard to handle properly
                        // so just skip further processing for now
                        return;
                    }
                }
                else
                {
                    arguments = invocationArguments.Skip(messageTemplateArgumentIndex + 1).Select(x =>
                    {
                        var location = x.GetLocation().SourceSpan;
                        return new SourceArgument(x.Expression, location.Start, location.Length);
                    }).ToList();
                    break;
                }
            }
        }

        if (arguments is null)
        {
            // we didn't get to the end because of errors, so abandon
            return;
        }

        // do properties match up?
        if (!hasErrors && literalSpan != default(TextSpan) && (arguments.Count > 0 || properties.Count > 0))
        {
            var diagnostics = PropertyBindingAnalyzer.AnalyzeProperties(properties, arguments);
            foreach (var diagnostic in diagnostics)
            {
                ReportDiagnostic(ref context, ref literalSpan, stringText, exactPositions, Diagnostics.PropertyBindingRule, diagnostic);
            }

            // check that all anonymous objects have destructuring hints in the message template
            if (arguments.Count == properties.Count)
            {
                for (var i = 0; i < arguments.Count; i++)
                {
                    var argument = arguments[i];
                    var argumentInfo = context.SemanticModel.GetTypeInfo(argument.Argument, context.CancellationToken);
                    if (argumentInfo.Type?.IsAnonymousType ?? false)
                    {
                        var property = properties[i];
                        if (!property.RawText.StartsWith("{@", StringComparison.Ordinal))
                        {
                            ReportDiagnostic(ref context, ref literalSpan, stringText, exactPositions, Diagnostics.DestructureAnonymousObjectsRule, new MessageTemplateDiagnostic(property.StartIndex, property.Length, property.PropertyName));
                        }
                    }
                }
            }

            // are there duplicate property names?
            var usedNames = new HashSet<string>();
            foreach (var property in properties)
            {
                if (!property.IsPositional && !usedNames.Add(property.PropertyName))
                {
                    ReportDiagnostic(ref context, ref literalSpan, stringText, exactPositions, Diagnostics.UniquePropertyNameRule, new MessageTemplateDiagnostic(property.StartIndex, property.Length, property.PropertyName));
                }

                var firstCharacter = property.PropertyName[0];
                if (!char.IsDigit(firstCharacter) && !char.IsUpper(firstCharacter))
                {
                    ReportDiagnostic(ref context, ref literalSpan, stringText, exactPositions, Diagnostics.PascalPropertyNameRule, new MessageTemplateDiagnostic(property.StartIndex, property.Length, property.PropertyName));
                }
            }
        }

        // is this an overload where the exception argument is used?
        var exception = context.Compilation.GetTypeByMetadataName("System.Exception");
        if (exception is null || HasConventionalExceptionParameter(exception, method))
        {
            return;
        }

        // is there an overload with the exception argument?
        var hasCandidateOverload = false;
        foreach (var x in method.ContainingType.GetMembers().OfType<IMethodSymbol>())
        {
            if (x.Name == method.Name && HasConventionalExceptionParameter(exception, x))
            {
                hasCandidateOverload = true;
                break;
            }
        }

        if (!hasCandidateOverload)
        {
            return;
        }

        // check whether any of the format arguments is an exception
        foreach (var argument in invocationArguments)
        {
            var argInfo = context.SemanticModel.GetTypeInfo(argument.Expression);
            if (IsException(exception, argInfo.Type))
            {
                context.ReportDiagnostic(Diagnostic.Create(Diagnostics.ExceptionRule, argument.GetLocation(), argument.Expression.ToFullString()));
            }
        }
    }

    private static void CheckForContextCorrectness(ref SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation, IMethodSymbol method)
    {
        // is this really a field / property?
        var decl = invocation.Ancestors().OfType<MemberDeclarationSyntax>().FirstOrDefault();
        if (!(decl is PropertyDeclarationSyntax || decl is FieldDeclarationSyntax))
        {
            return;
        }

        ITypeSymbol? contextType = null;

        if (method is { IsGenericMethod: true, TypeArguments.Length: 1 })
        {
            // extract T from GetLoggerFor<T>
            contextType = method.TypeArguments[0];
        }
        else if (method.Parameters.Length == 1 & method.Parameters[0].Type.ToString() == "System.Type")
        {
            // or extract T from GetLoggerFor(typeof(T))
            if (invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is TypeOfExpressionSyntax type
             && context.SemanticModel.GetTypeInfo(type.Type).Type is { } tSymbol)
            {
                contextType = tSymbol;
            }
        }

        // if there's no T...
        if (contextType == null)
        {
            return;
        }

        // find the type this field / property is contained in
        var declaringTypeSyntax = invocation.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        if (declaringTypeSyntax != null
         && context.SemanticModel.GetDeclaredSymbol(declaringTypeSyntax) is { } declaringType
         && !declaringType.Equals(contextType, SymbolEqualityComparer.Default))
        {
            // if there are multiple field / properties of ILogger, we can't be certain, so do nothing
            if (declaringType.GetMembers().Count(x => (x as IPropertySymbol)?.Type.ToString() == DatadogLoggerType || (x as IFieldSymbol)?.Type.ToString() == DatadogLoggerType) > 1)
            {
                return;
            }

            // get the location of T to report on
            Location? location;
            if (method.IsGenericMethod && invocation.Expression is MemberAccessExpressionSyntax { Name: GenericNameSyntax generic })
            {
                location = generic.TypeArgumentList.Arguments.FirstOrDefault()?.GetLocation();
            }
            else
            {
                location = (invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression as TypeOfExpressionSyntax)?.Type.GetLocation();
            }

            if (location is null)
            {
                // something went wrong
                return;
            }

            // get the name of the logger variable
            string? loggerName = null;
            var declaringMember = invocation.Ancestors().OfType<MemberDeclarationSyntax>().FirstOrDefault();
            if (declaringMember is PropertyDeclarationSyntax property)
            {
                loggerName = property.Identifier.ToString();
            }
            else if (declaringMember is FieldDeclarationSyntax field && field.Declaration.Variables.FirstOrDefault() is { } fieldVariable)
            {
                loggerName = fieldVariable.Identifier.ToString();
            }

            var correctMethod = method.IsGenericMethod ? $"{GetLoggerFor}<{declaringType}>()" : $"{GetLoggerFor}(typeof({declaringType}))";
            var incorrectMethod = method.IsGenericMethod ? $"{GetLoggerFor}<{contextType}>()" : $"{GetLoggerFor}(typeof({contextType}))";

            context.ReportDiagnostic(Diagnostic.Create(Diagnostics.UseCorrectContextualLoggerRule, location, loggerName, correctMethod, incorrectMethod));
        }
    }

    private static void ReportDiagnostic(ref SyntaxNodeAnalysisContext context, ref TextSpan literalSpan, string? stringText, bool exactPositions, DiagnosticDescriptor rule, MessageTemplateDiagnostic diagnostic)
    {
        TextSpan textSpan = default;
        if (diagnostic.MustBeRemapped)
        {
            if (!exactPositions)
            {
                textSpan = literalSpan;
            }
            else if (stringText != null)
            {
                int remappedStart = StringHelper.GetPositionInLiteral(stringText, diagnostic.StartIndex);
                int remappedEnd = StringHelper.GetPositionInLiteral(stringText, diagnostic.StartIndex + diagnostic.Length);
                textSpan = new TextSpan(literalSpan.Start + remappedStart, remappedEnd - remappedStart);
            }
        }
        else
        {
            textSpan = new TextSpan(diagnostic.StartIndex, diagnostic.Length);
        }

        var sourceLocation = Location.Create(context.Node.SyntaxTree, textSpan);
        context.ReportDiagnostic(Diagnostic.Create(rule, sourceLocation, diagnostic.Diagnostic));
    }

    // Check if there is an Exception parameter at position 1 (position 2 for static extension method invocations)?
    private static bool HasConventionalExceptionParameter(INamedTypeSymbol exceptionSymbol, IMethodSymbol methodSymbol)
    {
        return methodSymbol.IsExtensionMethod
                   ? (methodSymbol.Parameters.Length >= 2 && methodSymbol.Parameters[1].Type is { } e1 && e1.Equals(exceptionSymbol, SymbolEqualityComparer.Default))
                   : (methodSymbol.Parameters.Length >= 1 && methodSymbol.Parameters[0].Type is { } e2 && e2.Equals(exceptionSymbol, SymbolEqualityComparer.Default));
    }

    private static bool IsException(ITypeSymbol exceptionSymbol, ITypeSymbol? type)
    {
        var symbol = type;
        while (symbol is not null)
        {
            if (exceptionSymbol.Equals(symbol, SymbolEqualityComparer.Default))
            {
                return true;
            }

            symbol = symbol.BaseType;
        }

        return false;
    }
}
