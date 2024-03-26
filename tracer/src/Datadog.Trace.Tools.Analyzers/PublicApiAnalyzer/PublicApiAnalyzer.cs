// <copyright file="PublicApiAnalyzer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// Roughly based on https://github.com/dotnet/roslyn-analyzers/blob/4512de66bb6e21c548ab0d5a83242b70969ba576/src/NetAnalyzers/Core/Microsoft.NetCore.Analyzers/InteropServices/PlatformCompatibilityAnalyzer.cs
#nullable enable
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Datadog.Trace.Tools.Analyzers.PublicApiAnalyzer
{
    /// <summary>
    /// DD002: Incorrect usage of public API
    ///
    /// Finds internal usages of APIs specifically marked with the [PublicApi] flag.
    /// These methods should not be called directly by our library code, only users should invoke them.
    /// The analyzer enforces that. requirement
    ///
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class PublicApiAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// The diagnostic ID displayed in error messages
        /// </summary>
        public const string DiagnosticId = "DD0002";

        private const string PublicApiAttribute = nameof(PublicApiAttribute);

        private static readonly ImmutableArray<string> PublicApiAttributeNames = ImmutableArray.Create(PublicApiAttribute);

#pragma warning disable RS2008 // Enable analyzer release tracking for the analyzer project
        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            title: "Incorrect usage of public API",
            messageFormat: "This API is only for public usage and should not be called internally",
            category: "CodeQuality",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "This API is only for public usage and should not be called internally. Use an alternative method.");
#pragma warning restore RS2008

        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(context =>
            {
                var publicApiMembers = new ConcurrentDictionary<ISymbol, PublicApiStatus>(SymbolEqualityComparer.Default);

                context.RegisterOperationBlockStartAction(
                    context => AnalyzeOperationBlock(context, publicApiMembers));
            });
        }

        private void AnalyzeOperationBlock(
            OperationBlockStartAnalysisContext context,
            ConcurrentDictionary<ISymbol, PublicApiStatus> publicApiMembers)
        {
            var publicApiOperations = PooledConcurrentDictionary<KeyValuePair<IOperation, ISymbol>, bool>.GetInstance();

            context.RegisterOperationAction(
                context =>
                {
                    Helpers.AnalyzeOperation(context.Operation, publicApiOperations, publicApiMembers);
                },
                OperationKind.MethodReference,
                OperationKind.EventReference,
                OperationKind.FieldReference,
                OperationKind.Invocation,
                OperationKind.ObjectCreation,
                OperationKind.PropertyReference);

            context.RegisterOperationBlockEndAction(context =>
            {
                try
                {
                    if (publicApiOperations.IsEmpty)
                    {
                        return;
                    }

                    foreach (var kvp in publicApiOperations)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(Rule, kvp.Key.Key.Syntax.GetLocation()));
                    }
                }
                finally
                {
                    publicApiOperations.Free(context.CancellationToken);
                }
            });
        }

        private static class Helpers
        {
            internal static void AnalyzeOperation(
                IOperation operation,
                PooledConcurrentDictionary<KeyValuePair<IOperation, ISymbol>, bool> publicApiOperations,
                ConcurrentDictionary<ISymbol, PublicApiStatus> publicApiMembers)
            {
                var symbol = GetOperationSymbol(operation);

                if (symbol == null || (symbol is ITypeSymbol type && type.SpecialType != SpecialType.None))
                {
                    return;
                }

                CheckOperationAttributes(symbol, checkParents: true);

                if (symbol is IPropertySymbol property)
                {
                    foreach (var accessor in GetPropertyAccessors(property, operation))
                    {
                        if (accessor != null)
                        {
                            CheckOperationAttributes(accessor, checkParents: false);
                        }
                    }
                }
                else if (symbol is IEventSymbol iEvent)
                {
                    var accessor = GetEventAccessor(iEvent, operation);

                    if (accessor != null)
                    {
                        CheckOperationAttributes(accessor, checkParents: false);
                    }
                }
                else if (symbol is IMethodSymbol method && method.IsGenericMethod)
                {
                    CheckTypeArguments(method.TypeArguments);
                }

                if (symbol.ContainingSymbol is INamedTypeSymbol namedType && namedType.IsGenericType)
                {
                    CheckTypeArguments(namedType.TypeArguments);
                }

                void CheckTypeArguments(ImmutableArray<ITypeSymbol> typeArguments)
                {
                    using var workingSet = PooledHashSet<ITypeSymbol>.GetInstance();
                    CheckTypeArgumentsCore(typeArguments, workingSet);
                }

                void CheckTypeArgumentsCore(ImmutableArray<ITypeSymbol> typeArguments, PooledHashSet<ITypeSymbol> workingSet)
                {
                    foreach (var typeArgument in typeArguments)
                    {
                        if (!workingSet.Contains(typeArgument))
                        {
                            workingSet.Add(typeArgument);
                            if (typeArgument.SpecialType == SpecialType.None)
                            {
                                // Note: in the original, checkParents: true, but that causes
                                // issues for us when we have benign usages of the type parameter,
                                // as it flags usages of the type arguments _inside_
                                // the public method, as well as usages of Func<T> passed to the public method.
                                // We could use flow analysis to exclude those cases
                                // but that's more hassle than it's worth IMO.
                                CheckOperationAttributes(typeArgument, checkParents: false);

                                if (typeArgument is INamedTypeSymbol nType && nType.IsGenericType)
                                {
                                    CheckTypeArgumentsCore(nType.TypeArguments, workingSet);
                                }
                            }
                        }
                    }
                }
#pragma warning restore CS8321

                void CheckOperationAttributes(ISymbol symbol, bool checkParents)
                {
                    if (TryGetOrCreatePublicApiAttributes(symbol, checkParents, publicApiMembers, out _))
                    {
                        publicApiOperations.TryAdd(new KeyValuePair<IOperation, ISymbol>(operation, symbol), true);
                    }
                }
            }

            private static ISymbol? GetOperationSymbol(IOperation operation)
                => operation switch
                {
                    IInvocationOperation iOperation => iOperation.TargetMethod,
                    IObjectCreationOperation cOperation => cOperation.Constructor,
                    IFieldReferenceOperation fOperation => IsWithinConditionalOperation(fOperation) ? null : fOperation.Field,
                    IMemberReferenceOperation mOperation => mOperation.Member,
                    _ => null,
                };

            private static IEnumerable<ISymbol?> GetPropertyAccessors(IPropertySymbol property, IOperation operation)
            {
                var usageInfo = operation.GetValueUsageInfo(property.ContainingSymbol);

                // not checking/using ValueUsageInfo.Reference related values as property cannot be used as ref or out parameter
                // not using ValueUsageInfo.Name too, it only use name of the property
                if (usageInfo == ValueUsageInfo.ReadWrite)
                {
                    yield return property.GetMethod;
                    yield return property.SetMethod;
                }
                else if (usageInfo.IsWrittenTo())
                {
                    yield return property.SetMethod;
                }
                else if (usageInfo.IsReadFrom())
                {
                    yield return property.GetMethod;
                }
                else
                {
                    yield return property;
                }
            }

            private static ISymbol? GetEventAccessor(IEventSymbol iEvent, IOperation operation)
            {
                if (operation.Parent is IEventAssignmentOperation eventAssignment)
                {
                    return eventAssignment.Adds
                               ? iEvent.AddMethod
                               : iEvent.RemoveMethod;
                }

                return iEvent;
            }

            private static PublicApiStatus CopyAttributes(PublicApiStatus copyAttributes) =>
                new()
                {
                    IsAssemblyAttribute = copyAttributes.IsAssemblyAttribute,
                    IsPublicApi = copyAttributes.IsPublicApi
                };

            private static bool IsWithinConditionalOperation(IFieldReferenceOperation pOperation) =>
                pOperation.ConstantValue.HasValue &&
                pOperation.Parent is IBinaryOperation
                {
                    OperatorKind: BinaryOperatorKind.Equals
                    or BinaryOperatorKind.NotEquals
                    or BinaryOperatorKind.GreaterThan
                    or BinaryOperatorKind.LessThan
                    or BinaryOperatorKind.GreaterThanOrEqual
                    or BinaryOperatorKind.LessThanOrEqual
                };

            private static bool TryGetOrCreatePublicApiAttributes(
                ISymbol symbol,
                bool checkParents,
                ConcurrentDictionary<ISymbol, PublicApiStatus> publicApiMembers,
                out PublicApiStatus attributes)
            {
                if (!publicApiMembers.TryGetValue(symbol, out attributes))
                {
                    if (checkParents)
                    {
                        var container = symbol.ContainingSymbol;

                        // Namespaces do not have attributes
                        while (container is INamespaceSymbol)
                        {
                            container = container.ContainingSymbol;
                        }

                        if (container != null && TryGetOrCreatePublicApiAttributes(container, checkParents, publicApiMembers, out var containerAttributes))
                        {
                            attributes = CopyAttributes(containerAttributes);
                        }
                    }

                    attributes ??= new PublicApiStatus() { IsAssemblyAttribute = symbol is IAssemblySymbol };
                    MergePlatformAttributes(symbol.GetAttributes(), ref attributes);
                    attributes = publicApiMembers.GetOrAdd(symbol, attributes);
                }

                return attributes.IsPublicApi;

                static void MergePlatformAttributes(
                    ImmutableArray<AttributeData> immediateAttributes,
                    ref PublicApiStatus parentAttributes)
                {
                    foreach (AttributeData attribute in immediateAttributes)
                    {
                        if (PublicApiAttributeNames.Contains(attribute.AttributeClass!.Name))
                        {
                            parentAttributes.IsPublicApi = true;
                            return;
                        }
                    }
                }
            }
        }

        private sealed class PublicApiStatus
        {
            public bool IsAssemblyAttribute { get; set; }

            public bool IsPublicApi { get; set; }
        }
    }
}
