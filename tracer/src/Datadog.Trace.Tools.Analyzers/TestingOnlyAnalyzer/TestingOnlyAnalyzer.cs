// <copyright file="TestingOnlyAnalyzer.cs" company="Datadog">
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

namespace Datadog.Trace.Tools.Analyzers.TestingOnlyAnalyzer
{
    /// <summary>
    /// DD002: Incorrect usage of internal API
    ///
    /// Finds internal usages of APIs specifically marked with the [TestingOnly] or [TestingAndPrivateOnly] flag.
    /// - [TestingOnly]: Methods should not be called directly by our library code, only from test code.
    /// - [TestingAndPrivateOnly]: Methods can be called from within the same type, or from test code, but not from other types.
    /// The analyzer enforces these requirements.
    ///
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class TestingOnlyAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// The diagnostic ID displayed in error messages
        /// </summary>
        public const string DiagnosticId = "DD0002";

        private const string TestingOnlyAttribute = nameof(TestingOnlyAttribute);
        private const string TestingAndPrivateOnlyAttribute = nameof(TestingAndPrivateOnlyAttribute);

#pragma warning disable RS2008 // Enable analyzer release tracking for the analyzer project
        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            title: "Incorrect usage of internal API",
            messageFormat: "This API is only for use in tests and should not be called internally",
            category: "CodeQuality",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "This API is only for internal testing and should not be called internally. Use an alternative method.");
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
                var internalApiMembers = new ConcurrentDictionary<ISymbol, InternalApiStatus>(SymbolEqualityComparer.Default);

                context.RegisterOperationBlockStartAction(
                    ctx => AnalyzeOperationBlock(ctx, internalApiMembers));
            });
        }

        private static void AnalyzeOperationBlock(
            OperationBlockStartAnalysisContext context,
            ConcurrentDictionary<ISymbol, InternalApiStatus> internalApiMembers)
        {
            var internalApiOperations = PooledConcurrentDictionary<KeyValuePair<IOperation, ISymbol>, bool>.GetInstance();
            var callingType = context.OwningSymbol?.ContainingType;

            context.RegisterOperationAction(
                ctx =>
                {
                    Helpers.AnalyzeOperation(ctx.Operation, internalApiOperations, internalApiMembers, callingType);
                },
                OperationKind.MethodReference,
                OperationKind.EventReference,
                OperationKind.FieldReference,
                OperationKind.Invocation,
                OperationKind.ObjectCreation,
                OperationKind.PropertyReference);

            context.RegisterOperationBlockEndAction(ctx =>
            {
                try
                {
                    if (internalApiOperations.IsEmpty)
                    {
                        return;
                    }

                    foreach (var kvp in internalApiOperations)
                    {
                        ctx.ReportDiagnostic(Diagnostic.Create(Rule, kvp.Key.Key.Syntax.GetLocation()));
                    }
                }
                finally
                {
                    internalApiOperations.Free(ctx.CancellationToken);
                }
            });
        }

        private static class Helpers
        {
            internal static void AnalyzeOperation(
                IOperation operation,
                PooledConcurrentDictionary<KeyValuePair<IOperation, ISymbol>, bool> internalApiOperations,
                ConcurrentDictionary<ISymbol, InternalApiStatus> internalApiMembers,
                INamedTypeSymbol? callingType)
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
                    if (TryGetOrCreatePublicApiAttributes(symbol, checkParents, internalApiMembers, out var attributes))
                    {
                        // If the marked member has [TestingAndPrivateOnly], allow calls from within the same type
                        if (attributes.IsTestingAndPrivateOnly)
                        {
                            var targetType = symbol.ContainingType;

                            // Allow calls from within the same type
                            if (callingType != null && targetType != null &&
                                SymbolEqualityComparer.Default.Equals(callingType, targetType))
                            {
                                return;
                            }
                        }

                        internalApiOperations.TryAdd(new KeyValuePair<IOperation, ISymbol>(operation, symbol), true);
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

            private static InternalApiStatus CopyAttributes(InternalApiStatus copyAttributes) =>
                new()
                {
                    IsAssemblyAttribute = copyAttributes.IsAssemblyAttribute,
                    IsTestingApi = copyAttributes.IsTestingApi,
                    IsTestingAndPrivateOnly = copyAttributes.IsTestingAndPrivateOnly
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
                ConcurrentDictionary<ISymbol, InternalApiStatus> publicApiMembers,
                out InternalApiStatus attributes)
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

                    attributes ??= new InternalApiStatus() { IsAssemblyAttribute = symbol is IAssemblySymbol };
                    MergePlatformAttributes(symbol.GetAttributes(), ref attributes);
                    attributes = publicApiMembers.GetOrAdd(symbol, attributes);
                }

                return attributes.IsTestingApi;

                static void MergePlatformAttributes(
                    ImmutableArray<AttributeData> immediateAttributes,
                    ref InternalApiStatus parentAttributes)
                {
                    foreach (AttributeData attribute in immediateAttributes)
                    {
                        var attributeName = attribute.AttributeClass!.Name;
                        if (attributeName == TestingAndPrivateOnlyAttribute)
                        {
                            parentAttributes.IsTestingApi = true;
                            parentAttributes.IsTestingAndPrivateOnly = true;
                            return;
                        }
                        else if (attributeName == TestingOnlyAttribute)
                        {
                            parentAttributes.IsTestingApi = true;
                            parentAttributes.IsTestingAndPrivateOnly = false;
                            return;
                        }
                    }
                }
            }
        }

        private sealed class InternalApiStatus
        {
            public bool IsAssemblyAttribute { get; set; }

            public bool IsTestingApi { get; set; }

            public bool IsTestingAndPrivateOnly { get; set; }
        }
    }
}
