// <copyright file="DuckTypeAotRegistryAssemblyEmitter.MethodBinding.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Datadog.Trace.DuckTyping;
using dnlib.DotNet;

namespace Datadog.Trace.Tools.Runner.DuckTypeAot
{
    /// <summary>
    /// Member plans and method binding: the members of the proxy and target types as dynamic duck typing sees them (by name,
    /// binding flags, explicit interfaces, [DuckInclude]), the selection of the target method a proxy method calls (the default
    /// binder, duck chaining, optional parameters), and the conversions of its arguments and return value.
    /// </summary>
    internal static partial class DuckTypeAotRegistryAssemblyEmitter
    {
        /// <summary>
        /// Defines named constants for field accessor kind.
        /// </summary>
        private enum FieldAccessorKind
        {
            /// <summary>
            /// Represents getter.
            /// </summary>
            Getter,

            /// <summary>
            /// Represents setter.
            /// </summary>
            Setter
        }

        /// <summary>
        /// Defines named constants for method argument conversion kind.
        /// </summary>
        private enum MethodArgumentConversionKind
        {
            /// <summary>
            /// Represents none.
            /// </summary>
            None,

            /// <summary>
            /// Represents unwrap value with type.
            /// </summary>
            UnwrapValueWithType,

            /// <summary>
            /// Represents extract duck type instance.
            /// </summary>
            ExtractDuckTypeInstance,

            /// <summary>
            /// Represents duck chain to proxy.
            /// </summary>
            DuckChainToProxy,

            /// <summary>
            /// Represents type conversion.
            /// </summary>
            TypeConversion
        }

        /// <summary>
        /// Defines named constants for method return conversion kind.
        /// </summary>
        private enum MethodReturnConversionKind
        {
            /// <summary>
            /// Represents none.
            /// </summary>
            None,

            /// <summary>
            /// Represents wrap value with type.
            /// </summary>
            WrapValueWithType,

            /// <summary>
            /// Represents wrap value with type after duck-chaining the inner value.
            /// </summary>
            WrapValueWithTypeAfterDuckChainToProxy,

            /// <summary>
            /// Represents wrap value with type after applying type conversion to the inner value.
            /// </summary>
            WrapValueWithTypeAfterTypeConversion,

            /// <summary>
            /// Represents duck chain to proxy.
            /// </summary>
            DuckChainToProxy,

            /// <summary>
            /// Represents extract duck type instance.
            /// </summary>
            ExtractDuckTypeInstance,

            /// <summary>
            /// Represents type conversion.
            /// </summary>
            TypeConversion
        }

        /// <summary>
        /// Determines whether a duck typing attribute is one dynamic duck typing reads by type, so only the Datadog.Trace one.
        /// [DuckAsClass] is read by name, from any assembly (e.g. Datadog.Trace.Manual declares one).
        /// </summary>
        /// <param name="attributeTypeName">The full name of the attribute type.</param>
        /// <returns>true if dynamic duck typing reads the attribute by type; otherwise, false.</returns>
        private static bool IsDuckAttributeReadByType(string? attributeTypeName)
        {
            return attributeTypeName is DuckAttributeTypeName or DuckFieldAttributeTypeName or DuckPropertyOrFieldAttributeTypeName or DuckReverseMethodAttributeTypeName
                       or DuckCopyAttributeTypeName or DuckIgnoreAttributeTypeName or DuckIncludeAttributeTypeName;
        }

        /// <summary>
        /// Attempts to select a forward-mapping return-conversion strategy between target and proxy signatures.
        /// </summary>
        /// <param name="proxyReturnType">The proxy return type value.</param>
        /// <param name="targetReturnType">The target return type value.</param>
        /// <param name="returnConversion">The return conversion value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryCreateReturnConversion(TypeSig proxyReturnType, TypeSig targetReturnType, out MethodReturnConversion returnConversion)
            => TryCreateReturnConversion(proxyReturnType, targetReturnType, isReverseMapping: false, out returnConversion);

        /// <summary>
        /// Attempts to select a return-conversion strategy between target and proxy signatures.
        /// </summary>
        /// <param name="proxyReturnType">The proxy return type value.</param>
        /// <param name="targetReturnType">The target return type value.</param>
        /// <param name="isReverseMapping">The is reverse mapping value.</param>
        /// <param name="returnConversion">The return conversion value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryCreateReturnConversion(TypeSig proxyReturnType, TypeSig targetReturnType, bool isReverseMapping, out MethodReturnConversion returnConversion)
        {
            var cacheKeyStopwatch = StartProfilePhase();
            var conversionCacheKey = BuildMethodReturnConversionCacheKey(proxyReturnType, targetReturnType, isReverseMapping);
            StopProfilePhase(cacheKeyStopwatch, seconds => _currentProfile!.MethodReturnConversionCacheKeyBuildSeconds += seconds);
            if (_currentExecutionContext?.TryGetMethodReturnConversion(conversionCacheKey, out var cachedConversion) == true)
            {
                if (_currentProfile is not null)
                {
                    _currentProfile.ConversionPlanCacheHits++;
                }

                if (cachedConversion.Succeeded)
                {
                    returnConversion = cachedConversion.Conversion;
                    return true;
                }

                returnConversion = default;
                return false;
            }

            if (_currentProfile is not null)
            {
                _currentProfile.ConversionPlanCacheMisses++;
            }

            bool IsDuckChainingRequiredForMapping(TypeSig actualType, TypeSig expectedType)
            {
                // Dynamic reverse ducktyping swaps target/proxy order for duck-chaining checks.
                return isReverseMapping
                           ? IsDuckChainingRequired(expectedType, actualType)
                           : IsDuckChainingRequired(actualType, expectedType);
            }

            // Conversion precedence is deliberate to keep behavior stable:
            // exact match -> ValueWithType unwrap/wrap -> DuckChain -> primitive/reference conversion.
            if (AreTypesEquivalent(proxyReturnType, targetReturnType))
            {
                returnConversion = MethodReturnConversion.None();
                _currentExecutionContext?.CacheMethodReturnConversion(conversionCacheKey, new MethodReturnConversionCacheEntry(returnConversion));
                return true;
            }

            if (TryGetValueWithTypeArgument(proxyReturnType, out var proxyReturnValueWithTypeArgument))
            {
                var proxyInnerReturnType = proxyReturnValueWithTypeArgument!;
                if (AreTypesEquivalent(proxyInnerReturnType, targetReturnType))
                {
                    // ValueWithType<TProxy>: direct wrap when target returns TProxy.
                    returnConversion = MethodReturnConversion.WrapValueWithType(proxyReturnType, targetReturnType);
                    _currentExecutionContext?.CacheMethodReturnConversion(conversionCacheKey, new MethodReturnConversionCacheEntry(returnConversion));
                    return true;
                }

                if (IsDuckChainingRequiredForMapping(targetReturnType, proxyInnerReturnType))
                {
                    // ValueWithType<TProxy>: dynamic mode supports duck-chaining before wrapping.
                    returnConversion = MethodReturnConversion.WrapValueWithTypeAfterDuckChainToProxy(proxyReturnType, targetReturnType, isReverseMapping);
                    _currentExecutionContext?.CacheMethodReturnConversion(conversionCacheKey, new MethodReturnConversionCacheEntry(returnConversion));
                    return true;
                }

                if (CanUseTypeConversion(targetReturnType, proxyInnerReturnType))
                {
                    // ValueWithType<TProxy>: dynamic mode supports type conversion before wrapping.
                    returnConversion = MethodReturnConversion.WrapValueWithTypeAfterTypeConversion(proxyReturnType, targetReturnType);
                    _currentExecutionContext?.CacheMethodReturnConversion(conversionCacheKey, new MethodReturnConversionCacheEntry(returnConversion));
                    return true;
                }

                returnConversion = default;
                _currentExecutionContext?.CacheMethodReturnConversion(conversionCacheKey, new MethodReturnConversionCacheEntry());
                return false;
            }

            if (IsDuckChainingRequiredForMapping(targetReturnType, proxyReturnType))
            {
                returnConversion = MethodReturnConversion.DuckChainToProxy(proxyReturnType, targetReturnType, isReverseMapping);
                _currentExecutionContext?.CacheMethodReturnConversion(conversionCacheKey, new MethodReturnConversionCacheEntry(returnConversion));
                return true;
            }

            if (CanUseTypeConversion(targetReturnType, proxyReturnType))
            {
                returnConversion = MethodReturnConversion.TypeConversion(targetReturnType, proxyReturnType);
                _currentExecutionContext?.CacheMethodReturnConversion(conversionCacheKey, new MethodReturnConversionCacheEntry(returnConversion));
                return true;
            }

            returnConversion = default;
            _currentExecutionContext?.CacheMethodReturnConversion(conversionCacheKey, new MethodReturnConversionCacheEntry());
            return false;
        }

        /// <summary>
        /// Determines whether a safe type conversion can be emitted.
        /// </summary>
        /// <param name="actualTypeSig">The actual type sig value.</param>
        /// <param name="expectedTypeSig">The expected type sig value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool CanUseTypeConversion(TypeSig actualTypeSig, TypeSig expectedTypeSig)
        {
            // By-ref and open generic conversions are intentionally blocked to avoid emitting unverifiable IL.
            if (actualTypeSig.ElementType == ElementType.ByRef || expectedTypeSig.ElementType == ElementType.ByRef)
            {
                return false;
            }

            if (actualTypeSig.IsGenericParameter || expectedTypeSig.IsGenericParameter)
            {
                return actualTypeSig.IsGenericParameter && expectedTypeSig.IsGenericParameter && AreTypesEquivalent(actualTypeSig, expectedTypeSig);
            }

            var actualRuntimeType = TryResolveRuntimeType(actualTypeSig);
            var expectedRuntimeType = TryResolveRuntimeType(expectedTypeSig);
            // Runtime type resolution path centralizes assignability/enums/nullable handling across all mappings.
            if (actualRuntimeType is not null && expectedRuntimeType is not null)
            {
                return CanUseTypeConversion(actualRuntimeType, expectedRuntimeType);
            }

            var actualUnderlyingTypeSig = GetUnderlyingTypeForTypeConversion(actualTypeSig);
            var expectedUnderlyingTypeSig = GetUnderlyingTypeForTypeConversion(expectedTypeSig);
            if (AreTypesEquivalent(actualUnderlyingTypeSig, expectedUnderlyingTypeSig))
            {
                return true;
            }

            if (actualUnderlyingTypeSig.IsValueType)
            {
                if (expectedUnderlyingTypeSig.IsValueType)
                {
                    return false;
                }

                return IsObjectTypeSig(expectedUnderlyingTypeSig)
                    || IsTypeAssignableFrom(expectedUnderlyingTypeSig, actualUnderlyingTypeSig);
            }

            if (expectedUnderlyingTypeSig.IsValueType)
            {
                return IsObjectTypeSig(actualUnderlyingTypeSig)
                    || IsTypeAssignableFrom(actualUnderlyingTypeSig, expectedUnderlyingTypeSig);
            }

            return true;
        }

        /// <summary>
        /// Determines whether a safe type conversion can be emitted.
        /// </summary>
        /// <param name="actualType">The actual type value.</param>
        /// <param name="expectedType">The expected type value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool CanUseTypeConversion(Type actualType, Type expectedType)
        {
            var actualUnderlyingType = actualType.IsEnum ? Enum.GetUnderlyingType(actualType) : actualType;
            var expectedUnderlyingType = expectedType.IsEnum ? Enum.GetUnderlyingType(expectedType) : expectedType;

            if (actualUnderlyingType == expectedUnderlyingType)
            {
                return true;
            }

            if (actualUnderlyingType.IsValueType)
            {
                if (expectedUnderlyingType.IsValueType)
                {
                    return false;
                }

                return expectedUnderlyingType == typeof(object) || expectedUnderlyingType.IsAssignableFrom(actualUnderlyingType);
            }

            if (expectedUnderlyingType.IsValueType)
            {
                return actualUnderlyingType == typeof(object) || actualUnderlyingType.IsAssignableFrom(expectedUnderlyingType);
            }

            return true;
        }

        /// <summary>
        /// Gets underlying type for type conversion.
        /// </summary>
        /// <param name="typeSig">The type sig value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static TypeSig GetUnderlyingTypeForTypeConversion(TypeSig typeSig)
        {
            var typeDef = typeSig.ToTypeDefOrRef()?.ResolveTypeDef();
            if (typeDef?.IsEnum != true)
            {
                // Imported signatures belong to the generated module, whose resolver may not have the
                // source enum assembly. Use the runtime resolver already used to validate the conversion.
                var runtimeType = TryResolveRuntimeType(typeSig);
                if (runtimeType?.IsEnum == true && typeSig.Module is { } module)
                {
                    return RuntimeImporter(module).Import(Enum.GetUnderlyingType(runtimeType)).ToTypeSig();
                }

                return typeSig;
            }

            foreach (var field in typeDef.Fields)
            {
                if (field.IsSpecialName && string.Equals(field.Name, "value__", StringComparison.Ordinal))
                {
                    return field.FieldSig.Type;
                }
            }

            return typeSig;
        }

        /// <summary>
        /// Finds the property that declares the specified accessor.
        /// </summary>
        /// <param name="accessorMethod">The accessor method definition.</param>
        /// <returns>The declaring property when found; otherwise null.</returns>
        private static PropertyDef? FindPropertyFromAccessor(MethodDef accessorMethod)
        {
            var declaringType = accessorMethod.DeclaringType;
            if (declaringType is null)
            {
                return null;
            }

            var proxyTypePlan = _currentExecutionContext?.GetOrCreateProxyTypePlan(declaringType);
            if (proxyTypePlan?.TryGetPropertyFromAccessor(accessorMethod, out var cachedProperty) == true)
            {
                return cachedProperty;
            }

            var visitedTypes = new HashSet<string>(StringComparer.Ordinal);
            var typesToInspect = new Stack<TypeDef>();
            typesToInspect.Push(declaringType);

            while (typesToInspect.Count > 0)
            {
                var currentType = typesToInspect.Pop();
                // Graph walk includes base types and interfaces so inherited property metadata is preserved in emission.
                if (!visitedTypes.Add(BuildTypeIdentityKey(currentType)))
                {
                    continue;
                }

                foreach (var property in currentType.Properties)
                {
                    if (AccessorMatches(property.GetMethod, accessorMethod) || AccessorMatches(property.SetMethod, accessorMethod))
                    {
                        return property;
                    }
                }

                var baseType = currentType.BaseType?.ResolveTypeDef();
                if (baseType is not null)
                {
                    typesToInspect.Push(baseType);
                }

                foreach (var interfaceImpl in currentType.Interfaces)
                {
                    var resolvedInterface = interfaceImpl.Interface.ResolveTypeDef();
                    if (resolvedInterface is not null)
                    {
                        typesToInspect.Push(resolvedInterface);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Determines whether a method is declared directly on the inspected target type.
        /// </summary>
        /// <param name="method">The method definition.</param>
        /// <param name="declaringType">The target type definition.</param>
        /// <returns>true when the method belongs to the target type; otherwise false.</returns>
        private static bool IsMethodDeclaredOnType(MethodDef method, TypeDef declaringType)
        {
            return ReferenceEquals(method.DeclaringType, declaringType) ||
                   (method.DeclaringType is not null &&
                    string.Equals(BuildTypeIdentityKey(method.DeclaringType), BuildTypeIdentityKey(declaringType), StringComparison.Ordinal));
        }

        /// <summary>
        /// Resolves the declaring class's generic arguments through the proxy's base types.
        /// </summary>
        /// <param name="proxyClassType">The proxy class type value.</param>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="closedProxyTypeArguments">The closed arguments of the proxy class.</param>
        /// <returns>The arguments that close the method's declaring class.</returns>
        private static IReadOnlyList<TypeSig>? GetClassMethodGenericTypeArguments(
            TypeDef proxyClassType,
            MethodDef proxyMethod,
            IReadOnlyList<TypeSig>? closedProxyTypeArguments)
        {
            return GetClassMemberGenericTypeArguments(proxyClassType, proxyMethod.DeclaringType, closedProxyTypeArguments);
        }

        /// <summary>
        /// Gets the vtable slots a virtual method of a class hierarchy implements: the one it uses, and those of the base methods
        /// it explicitly overrides (e.g. a covariant return override).
        /// </summary>
        /// <param name="rootType">The most derived type of the hierarchy.</param>
        /// <param name="method">The virtual method.</param>
        /// <returns>The methods introducing the slots.</returns>
        private static IEnumerable<MethodDef> GetImplementedVtableSlotMethods(TypeDef rootType, MethodDef method)
        {
            yield return GetVtableSlotMethod(rootType, method);
            foreach (var methodOverride in method.Overrides)
            {
                if (methodOverride.MethodDeclaration.ResolveMethodDef() is { DeclaringType.IsInterface: false } overriddenMethod)
                {
                    yield return GetVtableSlotMethod(rootType, overriddenMethod);
                }
            }
        }

        /// <summary>
        /// Determines whether an inherited generic abstract method already has a concrete override.
        /// </summary>
        /// <param name="proxyClassType">The proxy class type.</param>
        /// <param name="proxyMethod">The inherited proxy method.</param>
        /// <returns>true if a concrete override exists.</returns>
        private static bool IsInheritedOpenGenericAbstractMethodWithConcreteOverride(TypeDef proxyClassType, MethodDef proxyMethod)
        {
            if (!proxyMethod.IsAbstract ||
                proxyMethod.DeclaringType is null ||
                ReferenceEquals(proxyMethod.DeclaringType, proxyClassType) ||
                proxyMethod.DeclaringType.GenericParameters.Count == 0)
            {
                return false;
            }

            var current = proxyClassType;
            while (current is not null && !ReferenceEquals(current, proxyMethod.DeclaringType))
            {
                foreach (var candidate in current.Methods)
                {
                    if (candidate.IsAbstract || !candidate.IsVirtual)
                    {
                        continue;
                    }

                    foreach (var methodOverride in candidate.Overrides)
                    {
                        var overriddenMethod = methodOverride.MethodDeclaration.ResolveMethodDef();
                        if (overriddenMethod is not null && MethodsMatch(overriddenMethod, proxyMethod))
                        {
                            return true;
                        }

                        if (overriddenMethod is null &&
                            string.Equals(methodOverride.MethodDeclaration.FullName, proxyMethod.FullName, StringComparison.Ordinal))
                        {
                            return true;
                        }
                    }
                }

                current = current.BaseType?.ResolveTypeDef();
            }

            return false;
        }

        /// <summary>
        /// Gets interface methods.
        /// </summary>
        /// <param name="interfaceType">The interface type value.</param>
        /// <param name="reverse">Whether the methods a reverse proxy implements are requested: like dynamic duck typing, they
        /// include the [DuckIgnore] methods, which a [DuckReverseMethod] method can still implement.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IReadOnlyList<MethodDef> GetInterfaceMethods(TypeDef interfaceType, bool reverse = false)
        {
            var proxyTypePlan = _currentExecutionContext?.GetOrCreateProxyTypePlan(interfaceType);
            return (reverse ? proxyTypePlan?.ReverseInterfaceMethods : proxyTypePlan?.InterfaceMethods) ?? Array.Empty<MethodDef>();
        }

        /// <summary>
        /// Gets class proxy methods.
        /// </summary>
        /// <param name="proxyClassType">The proxy class type value.</param>
        /// <param name="reverse">Whether the methods a reverse proxy overrides are requested: like dynamic duck typing, they
        /// include the [DuckIgnore] methods, which a [DuckReverseMethod] method can still implement.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IReadOnlyList<MethodDef> GetClassProxyMethods(TypeDef proxyClassType, bool reverse)
        {
            var proxyTypePlan = _currentExecutionContext?.GetOrCreateProxyTypePlan(proxyClassType);
            return (reverse ? proxyTypePlan?.ReverseClassMethods : proxyTypePlan?.ClassMethods) ?? Array.Empty<MethodDef>();
        }

        private static ProxyMethodPlan GetOrCreateProxyMethodPlan(MethodDef proxyMethod)
        {
            var declaringType = proxyMethod.DeclaringType;
            if (declaringType is null)
            {
                return new ProxyMethodPlan(proxyMethod);
            }

            return _currentExecutionContext?.GetOrCreateProxyTypePlan(declaringType).GetOrCreateMethodPlan(proxyMethod)
                ?? new ProxyMethodPlan(proxyMethod);
        }

        private static MethodPlan GetOrCreateMethodPlan(MethodDef method)
        {
            return _currentExecutionContext?.GetOrCreateMethodPlan(method)
                ?? new MethodPlan(method);
        }

        /// <summary>
        /// Gets the generic arguments closing the signature of a method the proxy implements: those of the proxy type, or for a
        /// [DuckInclude] method of the target type (or of a type it derives from), those of the target type.
        /// </summary>
        /// <param name="proxyType">The proxy type.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="proxyMethod">The method the proxy implements.</param>
        /// <param name="isInterfaceProxy">Whether the proxy type is an interface.</param>
        /// <param name="closedGenericProxyTypeArguments">The generic arguments of a closed generic proxy type.</param>
        /// <param name="closedGenericTargetTypeArguments">The generic arguments of a closed generic target type.</param>
        /// <returns>The generic arguments of the type declaring the method, or null.</returns>
        private static IReadOnlyList<TypeSig>? GetProxyMethodTypeArguments(
            TypeDef proxyType,
            TypeDef targetType,
            MethodDef proxyMethod,
            bool isInterfaceProxy,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments)
        {
            if (proxyMethod.DeclaringType is { } declaringType &&
                _currentExecutionContext?.GetOrCreateDuckIncludeMethods(targetType).Contains(proxyMethod) == true)
            {
                return GetClassMemberGenericTypeArguments(targetType, declaringType, closedGenericTargetTypeArguments);
            }

            return isInterfaceProxy ? closedGenericProxyTypeArguments : GetClassMethodGenericTypeArguments(proxyType, proxyMethod, closedGenericProxyTypeArguments);
        }

        /// <summary>
        /// Appends target methods explicitly marked with DuckInclude into the proxy method list.
        /// </summary>
        /// <param name="proxyMethods">The proxy methods value.</param>
        /// <param name="targetType">The target type value.</param>
        private static void AppendDuckIncludeTargetMethods(List<MethodDef> proxyMethods, TypeDef targetType)
        {
            var phaseStopwatch = StartProfilePhase();
            try
            {
                var visitedMethodKeys = new HashSet<string>(proxyMethods.Select(method => $"{method.Name}::{method.MethodSig}"), StringComparer.Ordinal);
                var duckIncludeMethods = _currentExecutionContext?.GetOrCreateDuckIncludeMethods(targetType) ?? Array.Empty<MethodDef>();
                foreach (var method in duckIncludeMethods)
                {
                    var key = $"{method.Name}::{method.MethodSig}";
                    if (visitedMethodKeys.Add(key))
                    {
                        proxyMethods.Add(method);
                    }
                }
            }
            finally
            {
                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.DuckIncludeCollectionSeconds += seconds);
            }
        }

        /// <summary>
        /// Determines whether supported class proxy method.
        /// </summary>
        /// <param name="method">The method value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool IsSupportedClassProxyMethod(MethodDef method, bool includeDuckIgnored)
        {
            if (method.IsConstructor || method.IsStatic || !method.IsVirtual || method.IsFinal)
            {
                return false;
            }

            if (string.Equals(method.DeclaringType?.FullName, "System.Object", StringComparison.Ordinal) &&
                !method.CustomAttributes.Any(attribute => string.Equals(attribute.TypeFullName, DuckIncludeAttributeTypeName, StringComparison.Ordinal)))
            {
                return false;
            }

            if (!method.IsPublic &&
                !method.IsFamily &&
                !method.IsAssembly &&
                !method.IsFamilyOrAssembly &&
                !method.IsFamilyAndAssembly)
            {
                return false;
            }

            return includeDuckIgnored || !IsDuckIgnoreMethod(method);
        }

        /// <summary>
        /// Attempts to detect incorrect forward usage of [DuckReverseMethod] on proxy members.
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="detail">The detail value.</param>
        /// <returns>true when [DuckReverseMethod] is used on a forward proxy member; otherwise, false.</returns>
        private static bool TryGetForwardReverseUsageFailure(MethodDef proxyMethod, out string? detail)
        {
            detail = null;
            if (proxyMethod.CustomAttributes.Any(IsReverseMethodAttribute))
            {
                detail = $"Proxy method '{proxyMethod.FullName}' is marked with [DuckReverseMethod] but forward mapping requires regular members.";
                return true;
            }

            if (TryGetDeclaringProperty(proxyMethod, out var declaringProperty) &&
                declaringProperty!.CustomAttributes.Any(IsReverseMethodAttribute))
            {
                detail = $"Proxy property '{declaringProperty.FullName}' is marked with [DuckReverseMethod] but forward mapping requires regular members.";
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether failure detail corresponds to invalid type conversion semantics.
        /// </summary>
        /// <param name="failureReason">The failure reason value.</param>
        /// <returns>true when the detail indicates invalid type conversion; otherwise, false.</returns>
        private static bool IsInvalidTypeConversionFailure(string failureReason)
        {
            return failureReason.IndexOf("Type conversion is not supported", StringComparison.Ordinal) >= 0 ||
                   failureReason.StartsWith("Invalid type conversion from ", StringComparison.Ordinal);
        }

        /// <summary>
        /// Determines whether failure detail corresponds to parameter-signature mismatch semantics.
        /// </summary>
        /// <param name="failureReason">The failure reason value.</param>
        /// <returns>true when the detail indicates parameter-signature mismatch; otherwise, false.</returns>
        private static bool IsParameterSignatureFailure(string failureReason)
        {
            return failureReason.IndexOf("Parameter count mismatch", StringComparison.Ordinal) >= 0 ||
                   failureReason.IndexOf("Parameter direction mismatch", StringComparison.Ordinal) >= 0 ||
                   failureReason.IndexOf("Parameter type mismatch", StringComparison.Ordinal) >= 0 ||
                   failureReason.IndexOf("By-ref parameter mismatch", StringComparison.Ordinal) >= 0 ||
                   failureReason.IndexOf("Generic arity mismatch", StringComparison.Ordinal) >= 0;
        }

        /// <summary>
        /// Extracts property name from a setter method candidate name.
        /// </summary>
        /// <param name="methodName">The method name value.</param>
        /// <returns>The resulting property name, or null when not a setter accessor.</returns>
        private static string? ExtractSetterPropertyName(string methodName)
        {
            const string setterPrefix = "set_";
            if (StringUtil.IsNullOrWhiteSpace(methodName))
            {
                return null;
            }

            var setterIndex = methodName.LastIndexOf(setterPrefix, StringComparison.Ordinal);
            if (setterIndex < 0)
            {
                return null;
            }

            var nameStart = setterIndex + setterPrefix.Length;
            if (nameStart >= methodName.Length)
            {
                return null;
            }

            return methodName.Substring(nameStart);
        }

        /// <summary>
        /// Determines whether property accessors are effectively private-only.
        /// </summary>
        /// <param name="property">The property value.</param>
        /// <returns>true when all available accessors are private; otherwise, false.</returns>
        private static bool IsPropertyPrivate(PropertyDef property)
        {
            var hasAccessor = property.GetMethod is not null || property.SetMethod is not null;
            if (!hasAccessor)
            {
                return false;
            }

            var getterPrivate = property.GetMethod is null || property.GetMethod.IsPrivate;
            var setterPrivate = property.SetMethod is null || property.SetMethod.IsPrivate;
            return getterPrivate && setterPrivate;
        }

        private static bool IsMethodCandidateAllowedByBindingFlags(TargetMethodCandidate candidate, BindingFlags bindingFlags, bool treatInheritedAsDeclared = false)
        {
            var method = candidate.Method;
            return IsMemberAllowedByBindingFlags(method.IsStatic, method.IsPublic, candidate.IsInherited && !treatInheritedAsDeclared, bindingFlags);
        }

        /// <summary>
        /// Finds forward-mapping target method candidates for a proxy method.
        /// </summary>
        /// <param name="mapping">The mapping value.</param>
        /// <param name="targetType">The target type value.</param>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="closedGenericMethodArguments">The closed generic method arguments value.</param>
        /// <param name="allowPrivateBaseMembers">The allow private base members value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IEnumerable<ForwardMethodCandidate> FindForwardTargetMethodCandidates(
            DuckTypeAotMapping mapping,
            TypeDef targetType,
            ProxyMethodPlan proxyMethodPlan,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericMethodArguments,
            bool allowPrivateBaseMembers)
        {
            var proxyMethod = proxyMethodPlan.Method;
            var explicitInterfaceTypeNames = proxyMethodPlan.ExplicitInterfaceTypeNames;
            var useRelaxedNameComparison = proxyMethodPlan.UseRelaxedNameComparison;
            var configuredParameterTypeNames = proxyMethodPlan.ConfiguredParameterTypeNames;
            var expectedGenericArity = closedGenericMethodArguments?.Count ?? (int)proxyMethod.MethodSig.GenParamCount;

            if (mapping.Mode == DuckTypeAotMappingMode.Reverse)
            {
                var emittedCandidates = new HashSet<string>(StringComparer.Ordinal);
                foreach (var reverseCandidate in FindReverseTargetMethodCandidates(targetType, proxyMethod, closedGenericProxyTypeArguments, closedGenericTargetTypeArguments))
                {
                    // Candidate must match both generic arity and parameter count before deeper compatibility checks.
                    if (reverseCandidate.MethodSig.GenParamCount != expectedGenericArity ||
                        reverseCandidate.MethodSig.Params.Count != proxyMethod.MethodSig.Params.Count)
                    {
                        continue;
                    }

                    var reverseCandidateKey = GetMethodCandidateKey(reverseCandidate);
                    if (emittedCandidates.Add(reverseCandidateKey))
                    {
                        yield return new ForwardMethodCandidate(reverseCandidate, nameOrdinal: 0);
                    }
                }

                yield break;
            }

            foreach (var candidate in FindDefaultTargetMethodCandidates(
                         targetType,
                         proxyMethodPlan,
                         explicitInterfaceTypeNames,
                         useRelaxedNameComparison,
                         expectedGenericArity,
                         configuredParameterTypeNames,
                         allowPrivateBaseMembers,
                         allowTrailingOptionalTargetParameters: true))
            {
                yield return candidate;
            }
        }

        /// <summary>
        /// Determines whether reverse method attributes are present on the target type hierarchy.
        /// </summary>
        /// <param name="targetType">The target type value.</param>
        /// <returns>true when any method or property has [DuckReverseMethod]; otherwise, false.</returns>
        private static bool HasReverseMethodAttributes(TypeDef targetType)
        {
            var targetTypePlan = _currentExecutionContext?.GetOrCreateTargetTypePlan(targetType);
            return targetTypePlan?.HasReverseMethodAttributes == true;
        }

        /// <summary>
        /// Finds default target method candidates by name and signature prefilters.
        /// </summary>
        /// <param name="targetType">The target type value.</param>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="explicitInterfaceTypeNames">The explicit interface type names value.</param>
        /// <param name="useRelaxedNameComparison">The use relaxed name comparison value.</param>
        /// <param name="expectedGenericArity">The expected generic arity value.</param>
        /// <param name="allowPrivateBaseMembers">The allow private base members value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IEnumerable<ForwardMethodCandidate> FindDefaultTargetMethodCandidates(
            TypeDef targetType,
            ProxyMethodPlan proxyMethodPlan,
            IReadOnlyList<string> explicitInterfaceTypeNames,
            bool useRelaxedNameComparison,
            int expectedGenericArity,
            IReadOnlyList<string> configuredParameterTypeNames,
            bool allowPrivateBaseMembers,
            bool allowTrailingOptionalTargetParameters)
        {
            // The candidates come from the plan of the target type, which only exists while emitting.
            var targetTypePlan = _currentExecutionContext?.GetOrCreateTargetTypePlan(targetType);
            if (targetTypePlan is null)
            {
                yield break;
            }

            foreach (var candidate in targetTypePlan.GetForwardMethodCandidates(
                         proxyMethodPlan,
                         explicitInterfaceTypeNames,
                         useRelaxedNameComparison,
                         expectedGenericArity,
                         configuredParameterTypeNames,
                         allowPrivateBaseMembers,
                         allowTrailingOptionalTargetParameters))
            {
                yield return candidate;
            }
        }

        /// <summary>
        /// Selects a target method like Type.GetMethod with the proxy's parameter types (its default binder) does, for each name
        /// of the proxy method in order: a method with as many parameters, each of which accepts the proxy's (the same type, a
        /// type it's assignable to, object, or a wider primitive), the most specific one.
        /// </summary>
        /// <param name="mapping">The mapping.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="proxyMethodPlan">The proxy method plan.</param>
        /// <param name="closedGenericProxyTypeArguments">The generic arguments of a closed generic proxy type.</param>
        /// <param name="closedGenericTargetTypeArguments">The generic arguments of a closed generic target type.</param>
        /// <param name="allowPrivateBaseMethodCandidates">Whether private methods of base types are candidates.</param>
        /// <param name="selectedMethod">The selected method.</param>
        /// <returns>true if the binder selects a method; false if it selects none, or several (it throws then, which the
        /// candidate scan reports as an ambiguity).</returns>
        private static bool TrySelectWithDefaultBinder(
            DuckTypeAotMapping mapping,
            TypeDef targetType,
            ProxyMethodPlan proxyMethodPlan,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments,
            bool allowPrivateBaseMethodCandidates,
            out MethodDef? selectedMethod)
        {
            selectedMethod = null;
            var proxyMethod = proxyMethodPlan.Method;
            var proxyParameterTypes = proxyMethod.MethodSig.Params
                                                 .Select(parameter => SubstituteTypeAndMethodGenericTypeArguments(parameter, closedGenericProxyTypeArguments, closedGenericMethodArguments: null))
                                                 .ToList();
            var nameComparison = proxyMethodPlan.UseIgnoreCaseMemberMatching ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            foreach (var candidatesOfName in FindForwardTargetMethodCandidates(mapping, targetType, proxyMethodPlan, closedGenericProxyTypeArguments, closedGenericTargetTypeArguments, closedGenericMethodArguments: null, allowPrivateBaseMethodCandidates)
                                                .GroupBy(candidate => candidate.NameOrdinal)
                                                .OrderBy(group => group.Key))
            {
                var name = candidatesOfName.Key < proxyMethodPlan.ForwardTargetMethodNames.Count ? proxyMethodPlan.ForwardTargetMethodNames[candidatesOfName.Key] : null;
                MethodDef? best = null;
                IReadOnlyList<TypeSig>? bestParameterTypes = null;
                var ambiguous = false;
                foreach (var candidate in candidatesOfName)
                {
                    var method = candidate.Method;
                    if (name is null ||
                        !string.Equals(method.Name.String, name, nameComparison) ||
                        method.MethodSig.GenParamCount != 0 ||
                        method.MethodSig.Params.Count != proxyParameterTypes.Count)
                    {
                        continue;
                    }

                    var declaringTypeArguments = GetClassMethodGenericTypeArguments(targetType, method, closedGenericTargetTypeArguments);
                    var parameterTypes = method.MethodSig.Params
                                               .Select(parameter => SubstituteTypeAndMethodGenericTypeArguments(parameter, declaringTypeArguments, closedGenericMethodArguments: null))
                                               .ToList();
                    if (!parameterTypes.Select((parameterType, index) => IsDefaultBinderArgumentCompatible(proxyParameterTypes[index], parameterType)).All(compatible => compatible))
                    {
                        continue;
                    }

                    if (best is null)
                    {
                        best = method;
                        bestParameterTypes = parameterTypes;
                        continue;
                    }

                    // The binder's most specific method: by parameter, then by declaring type (the most derived).
                    var specificity = CompareDefaultBinderSpecificity(parameterTypes, bestParameterTypes!, proxyParameterTypes);
                    if (specificity == 0)
                    {
                        specificity = IsAssignableFrom(best.DeclaringType, method.DeclaringType) && !IsSameMetadataType(best.DeclaringType, method.DeclaringType) ? 1 :
                                      IsAssignableFrom(method.DeclaringType, best.DeclaringType) && !IsSameMetadataType(best.DeclaringType, method.DeclaringType) ? -1 : 0;
                    }

                    if (specificity > 0)
                    {
                        best = method;
                        bestParameterTypes = parameterTypes;
                        ambiguous = false;
                    }
                    else if (specificity == 0)
                    {
                        ambiguous = true;
                    }
                }

                if (best is not null)
                {
                    selectedMethod = ambiguous ? null : best;
                    return !ambiguous;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether a parameter accepts an argument of a type for the default binder of Type.GetMethod: the same type,
        /// object, a type the argument's type is assignable to, or a wider primitive (e.g. long for int). By-ref parameters need
        /// the same type.
        /// </summary>
        /// <param name="argumentType">The type of the argument (the proxy's parameter).</param>
        /// <param name="parameterType">The type of the parameter.</param>
        /// <returns>true if the binder accepts it; otherwise, false.</returns>
        private static bool IsDefaultBinderArgumentCompatible(TypeSig argumentType, TypeSig parameterType)
        {
            if (AreTypesEquivalent(argumentType, parameterType))
            {
                return true;
            }

            if (argumentType.ElementType == ElementType.ByRef || parameterType.ElementType == ElementType.ByRef ||
                argumentType.IsGenericParameter || parameterType.IsGenericParameter)
            {
                return false;
            }

            return IsObjectTypeSig(parameterType) ||
                   IsTypeAssignableFrom(parameterType, argumentType) ||
                   IsPrimitiveWidening(argumentType.ElementType, parameterType.ElementType);
        }

        /// <summary>
        /// Compares the specificity of two parameter lists for arguments of given types, like the default binder: 1 if the first
        /// is more specific, -1 if the second is, 0 if neither is.
        /// </summary>
        /// <param name="first">The first parameter list.</param>
        /// <param name="second">The second parameter list.</param>
        /// <param name="argumentTypes">The argument types.</param>
        /// <returns>The comparison.</returns>
        private static int CompareDefaultBinderSpecificity(IReadOnlyList<TypeSig> first, IReadOnlyList<TypeSig> second, IReadOnlyList<TypeSig> argumentTypes)
        {
            var firstIsMoreSpecific = false;
            var secondIsMoreSpecific = false;
            for (var i = 0; i < first.Count; i++)
            {
                if (AreTypesEquivalent(first[i], second[i]))
                {
                    continue;
                }

                if (AreTypesEquivalent(first[i], argumentTypes[i]) ||
                    IsObjectTypeSig(second[i]) ||
                    IsTypeAssignableFrom(second[i], first[i]) ||
                    IsPrimitiveWidening(first[i].ElementType, second[i].ElementType))
                {
                    firstIsMoreSpecific = true;
                }
                else if (AreTypesEquivalent(second[i], argumentTypes[i]) ||
                         IsObjectTypeSig(first[i]) ||
                         IsTypeAssignableFrom(first[i], second[i]) ||
                         IsPrimitiveWidening(second[i].ElementType, first[i].ElementType))
                {
                    secondIsMoreSpecific = true;
                }
            }

            return firstIsMoreSpecific == secondIsMoreSpecific ? 0 : firstIsMoreSpecific ? 1 : -1;
        }

        /// <summary>
        /// Determines whether a primitive type widens to another one for the default binder (e.g. int to long, float to double).
        /// </summary>
        /// <param name="source">The source element type.</param>
        /// <param name="target">The target element type.</param>
        /// <returns>true if the source widens to the target; otherwise, false.</returns>
        private static bool IsPrimitiveWidening(ElementType source, ElementType target)
            => source switch
            {
                ElementType.Char => target is ElementType.U2 or ElementType.U4 or ElementType.I4 or ElementType.U8 or ElementType.I8 or ElementType.R4 or ElementType.R8,
                ElementType.U1 => target is ElementType.Char or ElementType.U2 or ElementType.I2 or ElementType.U4 or ElementType.I4 or ElementType.U8 or ElementType.I8 or ElementType.R4 or ElementType.R8,
                ElementType.I1 => target is ElementType.I2 or ElementType.I4 or ElementType.I8 or ElementType.R4 or ElementType.R8,
                ElementType.U2 => target is ElementType.U4 or ElementType.I4 or ElementType.U8 or ElementType.I8 or ElementType.R4 or ElementType.R8,
                ElementType.I2 => target is ElementType.I4 or ElementType.I8 or ElementType.R4 or ElementType.R8,
                ElementType.U4 => target is ElementType.U8 or ElementType.I8 or ElementType.R4 or ElementType.R8,
                ElementType.I4 => target is ElementType.I8 or ElementType.R4 or ElementType.R8,
                ElementType.U8 or ElementType.I8 => target is ElementType.R4 or ElementType.R8,
                ElementType.R4 => target is ElementType.R8,
                _ => false,
            };

        /// <summary>
        /// Gets the failure dynamic duck typing has converting the arguments (then the return value) of a proxy method for the
        /// target method it selects: "Invalid type conversion from {source} to {target}".
        /// </summary>
        /// <param name="proxyMethod">The proxy method.</param>
        /// <param name="targetMethod">The target method.</param>
        /// <param name="closedGenericProxyTypeArguments">The generic arguments of a closed generic proxy type.</param>
        /// <param name="closedGenericTargetTypeArguments">The generic arguments of the target method's declaring type.</param>
        /// <returns>The failure detail, or null when every conversion is supported.</returns>
        private static string? GetInvalidTypeConversionDetail(MethodDef proxyMethod, MethodDef targetMethod, IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments, IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments)
        {
            for (var i = 0; i < proxyMethod.MethodSig.Params.Count && i < targetMethod.MethodSig.Params.Count; i++)
            {
                var proxyParameterType = SubstituteTypeAndMethodGenericTypeArguments(proxyMethod.MethodSig.Params[i], closedGenericProxyTypeArguments, closedGenericMethodArguments: null);
                var targetParameterType = SubstituteTypeAndMethodGenericTypeArguments(targetMethod.MethodSig.Params[i], closedGenericTargetTypeArguments, closedGenericMethodArguments: null);
                if (proxyParameterType.ElementType != ElementType.ByRef &&
                    targetParameterType.ElementType != ElementType.ByRef &&
                    !IsDuckChainingRequired(targetParameterType, proxyParameterType) &&
                    !CanUseTypeConversion(proxyParameterType, targetParameterType))
                {
                    return $"Invalid type conversion from {GetRuntimeFullName(proxyParameterType)} to {GetRuntimeFullName(targetParameterType)}";
                }
            }

            var proxyReturnType = SubstituteTypeAndMethodGenericTypeArguments(proxyMethod.MethodSig.RetType, closedGenericProxyTypeArguments, closedGenericMethodArguments: null);
            var targetReturnType = SubstituteTypeAndMethodGenericTypeArguments(targetMethod.MethodSig.RetType, closedGenericTargetTypeArguments, closedGenericMethodArguments: null);
            if (proxyReturnType.ElementType != ElementType.Void &&
                targetReturnType.ElementType != ElementType.Void &&
                !IsDuckChainingRequired(proxyReturnType, targetReturnType) &&
                !CanUseTypeConversion(targetReturnType, proxyReturnType))
            {
                return $"Invalid type conversion from {GetRuntimeFullName(targetReturnType)} to {GetRuntimeFullName(proxyReturnType)}";
            }

            return null;

            static string GetRuntimeFullName(TypeSig type) => TryResolveRuntimeType(type)?.FullName ?? type.ReflectionFullName;
        }

        /// <summary>
        /// Determines whether candidate method parameters match configured [Duck(ParameterTypeNames=...)] values.
        /// </summary>
        /// <param name="candidate">The candidate value.</param>
        /// <param name="configuredParameterTypeNames">The configured parameter type names value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool IsForwardCandidateParameterTypeNameMatch(MethodDef candidate, IReadOnlyList<string> configuredParameterTypeNames, IReadOnlyList<TypeSig>? closedGenericTypeArguments)
        {
            if (configuredParameterTypeNames.Count != candidate.MethodSig.Params.Count)
            {
                return false;
            }

            for (var i = 0; i < configuredParameterTypeNames.Count; i++)
            {
                var configuredName = configuredParameterTypeNames[i];
                if (StringUtil.IsNullOrWhiteSpace(configuredName))
                {
                    return false;
                }

                var parameterType = SubstituteTypeAndMethodGenericTypeArguments(candidate.MethodSig.Params[i], closedGenericTypeArguments, closedGenericMethodArguments: null);
                if (!GetTypeComparisonNames(parameterType).Contains(configuredName))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Gets method candidate key.
        /// </summary>
        /// <param name="candidate">The candidate value.</param>
        /// <returns>The resulting string value.</returns>
        private static string GetMethodCandidateKey(MethodDef candidate)
        {
            return BuildMethodIdentityKey(candidate);
        }

        private static string BuildForwardMethodBindingPlanCacheKey(
            ProxyMethodPlan proxyMethodPlan,
            MethodPlan targetMethodPlan,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericMethodArguments,
            bool isReverseMapping,
            bool enforceMethodSelectionRules)
        {
            return string.Concat(
                isReverseMapping ? "reverse" : "forward",
                enforceMethodSelectionRules ? "|strict|" : "|selected|",
                proxyMethodPlan.IdentityKey,
                "|",
                targetMethodPlan.IdentityKey,
                "|",
                BuildTypeSigSequenceCacheKey(closedGenericProxyTypeArguments),
                "|",
                BuildTypeSigSequenceCacheKey(closedGenericTargetTypeArguments),
                "|",
                BuildTypeSigSequenceCacheKey(closedGenericMethodArguments));
        }

        private static string BuildMethodReturnConversionCacheKey(TypeSig proxyReturnType, TypeSig targetReturnType, bool isReverseMapping, string discriminator = "")
        {
            return string.Concat(
                discriminator,
                "|",
                isReverseMapping ? "reverse" : "forward",
                "|",
                BuildTypeSigCacheKey(proxyReturnType),
                "|",
                BuildTypeSigCacheKey(targetReturnType));
        }

        private static string BuildMethodArgumentConversionCacheKey(TypeSig proxyParameterType, TypeSig targetParameterType, bool isReverseMapping, bool enforceMethodSelectionRules)
        {
            return string.Concat(
                isReverseMapping ? "reverse" : "forward",
                "|",
                enforceMethodSelectionRules ? "strict" : "loose",
                "|",
                BuildTypeSigCacheKey(proxyParameterType),
                "|",
                BuildTypeSigCacheKey(targetParameterType));
        }

        /// <summary>
        /// Finds reverse-mapping target method candidates for a proxy method.
        /// </summary>
        /// <param name="targetType">The target type value.</param>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IEnumerable<MethodDef> FindReverseTargetMethodCandidates(
            TypeDef targetType,
            MethodDef proxyMethod,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments)
        {
            var proxyMethodName = proxyMethod.Name.String ?? proxyMethod.Name.ToString();
            var proxyParameterTypes = proxyMethod.MethodSig.Params.ToArray();
            var proxyParameterTypeNames = proxyMethod.MethodSig.Params
                                              .Select(GetTypeComparisonNames)
                                              .ToArray();
            var emittedCandidates = new HashSet<string>(StringComparer.Ordinal);

            foreach (var method in targetType.Methods)
            {
                if (method.IsConstructor || method.IsStatic)
                {
                    continue;
                }

                foreach (var reverseAttribute in GetReverseMethodAttributes(method))
                {
                    if (!IsReverseCandidateMatch(proxyMethodName, proxyParameterTypes, proxyParameterTypeNames, reverseAttribute, method.Name.String ?? method.Name.ToString()))
                    {
                        continue;
                    }

                    if (!TryGetDuckAttributeParameterTypeNames(reverseAttribute, out _) &&
                        !MatchesReverseMethodParameterSelection(
                            proxyMethod,
                            method,
                            closedGenericProxyTypeArguments,
                            GetClassMethodGenericTypeArguments(targetType, method, closedGenericTargetTypeArguments)))
                    {
                        continue;
                    }

                    var candidateKey = $"{method.DeclaringType.FullName}::{method.Name}::{method.MethodSig}";
                    if (emittedCandidates.Add(candidateKey))
                    {
                        yield return method;
                    }
                }
            }

            var current = targetType;
            while (current is not null)
            {
                foreach (var property in current.Properties)
                {
                    if (!IsReverseImplementationPropertyVisibleToDynamic(property))
                    {
                        continue;
                    }

                    foreach (var reverseAttribute in property.CustomAttributes.Where(IsReverseMethodAttribute))
                    {
                        if (property.GetMethod is not null && IsReverseCandidateMatch(proxyMethodName, proxyParameterTypes, proxyParameterTypeNames, reverseAttribute, "get_" + property.Name))
                        {
                            var candidateKey = $"{property.GetMethod.DeclaringType.FullName}::{property.GetMethod.Name}::{property.GetMethod.MethodSig}";
                            if (emittedCandidates.Add(candidateKey))
                            {
                                yield return property.GetMethod;
                            }
                        }

                        if (property.SetMethod is not null && IsReverseCandidateMatch(proxyMethodName, proxyParameterTypes, proxyParameterTypeNames, reverseAttribute, "set_" + property.Name))
                        {
                            var candidateKey = $"{property.SetMethod.DeclaringType.FullName}::{property.SetMethod.Name}::{property.SetMethod.MethodSig}";
                            if (emittedCandidates.Add(candidateKey))
                            {
                                yield return property.SetMethod;
                            }
                        }
                    }
                }

                current = current.BaseType?.ResolveTypeDef();
            }
        }

        private static bool MatchesReverseMethodParameterSelection(
            MethodDef proxyMethod,
            MethodDef implementationMethod,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericImplementationTypeArguments)
        {
            if (proxyMethod.MethodSig.Params.Count != implementationMethod.MethodSig.Params.Count)
            {
                return false;
            }

            for (var index = 0; index < proxyMethod.MethodSig.Params.Count; index++)
            {
                var contractParameter = SubstituteTypeAndMethodGenericTypeArguments(proxyMethod.MethodSig.Params[index], closedGenericProxyTypeArguments, closedGenericMethodArguments: null);
                var implementationParameter = SubstituteTypeAndMethodGenericTypeArguments(implementationMethod.MethodSig.Params[index], closedGenericImplementationTypeArguments, closedGenericMethodArguments: null);
                if (contractParameter is ByRefSig contractByRef && implementationParameter is ByRefSig implementationByRef)
                {
                    contractParameter = contractByRef.Next;
                    implementationParameter = implementationByRef.Next;
                }

                if (!MatchesDynamicMethodParameterSelectionRule(implementationParameter, contractParameter))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Reads reverse method metadata with the attribute inheritance used by dynamic reverse proxies.
        /// </summary>
        /// <param name="method">The implementation method declared on the delegation type.</param>
        /// <returns>The effective reverse attributes, including attributes inherited by virtual overrides.</returns>
        private static IEnumerable<CustomAttribute> GetReverseMethodAttributes(MethodDef method)
        {
            var rootType = method.DeclaringType;
            var currentMethod = method;
            while (currentMethod is not null)
            {
                var attributes = currentMethod.CustomAttributes.Where(IsReverseMethodAttribute).ToArray();
                if (attributes.Length > 0)
                {
                    return attributes;
                }

                if (!currentMethod.IsVirtual || currentMethod.IsNewSlot)
                {
                    break;
                }

                var currentArguments = GetClassMemberGenericTypeArguments(rootType, currentMethod.DeclaringType, closedRootTypeArguments: null);
                var baseType = currentMethod.DeclaringType.BaseType?.ResolveTypeDef();
                MethodDef? baseMethod = null;
                while (baseType is not null && baseMethod is null)
                {
                    var baseArguments = GetClassMemberGenericTypeArguments(rootType, baseType, closedRootTypeArguments: null);
                    baseMethod = baseType.Methods.FirstOrDefault(candidate =>
                        candidate.IsVirtual &&
                        !candidate.IsStatic &&
                        string.Equals(candidate.Name, currentMethod.Name, StringComparison.Ordinal) &&
                        AreEffectiveMethodSignaturesEquivalent(candidate.MethodSig, baseArguments, currentMethod.MethodSig, currentArguments));
                    baseType = baseType.BaseType?.ResolveTypeDef();
                }

                currentMethod = baseMethod;
            }

            return Array.Empty<CustomAttribute>();
        }

        /// <summary>
        /// Checks whether dynamic reverse duck typing would consider the implementation property.
        /// </summary>
        /// <param name="property">Implementation property.</param>
        /// <returns><c>true</c> when the property has at least one public accessor.</returns>
        private static bool IsReverseImplementationPropertyVisibleToDynamic(PropertyDef property)
        {
            return property.GetMethod?.IsPublic == true || property.SetMethod?.IsPublic == true;
        }

        /// <summary>
        /// Determines whether the generator's dynamic duck typing reads an attribute like the application's does: a duck typing
        /// attribute it reads by type, declared by another assembly (standalone contracts can declare their own) or by another
        /// copy of Datadog.Trace, is invisible to it. [DuckAsClass] is read by name, and other attributes (e.g. the [DuckType]
        /// of Datadog.Trace.Manual) don't matter.
        /// </summary>
        /// <param name="attributeType">The attribute type.</param>
        /// <returns>true if dynamic duck typing in the generator reads the attribute as it is meant to; otherwise, false.</returns>
        private static bool IsReadByGeneratorDuckTyping(Type attributeType)
        {
            return attributeType.Assembly == typeof(DuckType).Assembly || !IsDuckAttributeReadByType(attributeType.FullName);
        }

        /// <summary>
        /// Finds the runtime method of a type (including base types and interfaces) defined by a metadata method.
        /// </summary>
        /// <param name="runtimeType">The runtime type value.</param>
        /// <param name="method">The metadata method value.</param>
        /// <returns>The runtime method, or null when it isn't found.</returns>
        private static MethodInfo? FindRuntimeMethod(Type runtimeType, MethodDef method)
        {
            if (method.Module?.Mvid is not { } moduleVersionId)
            {
                return null;
            }

            var metadataToken = unchecked((int)method.MDToken.Raw);
            foreach (var type in EnumerateRuntimeTypeHierarchy(runtimeType))
            {
                foreach (var runtimeMethod in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (runtimeMethod.MetadataToken == metadataToken && runtimeMethod.Module.ModuleVersionId == moduleVersionId)
                    {
                        return runtimeMethod;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the metadata method of a type hierarchy that defines a runtime method.
        /// </summary>
        /// <param name="type">The type definition value.</param>
        /// <param name="runtimeMethod">The runtime method value.</param>
        /// <returns>The metadata method, or null when it isn't found.</returns>
        private static MethodDef? FindMethodDefInHierarchy(TypeDef type, MethodInfo runtimeMethod)
        {
            var methodDefinition = runtimeMethod.IsGenericMethod && !runtimeMethod.IsGenericMethodDefinition
                                       ? runtimeMethod.GetGenericMethodDefinition()
                                       : runtimeMethod;
            return FindMemberDefInHierarchy(type, methodDefinition, static current => current.Methods);
        }

        /// <summary>
        /// Determines whether forward target method name match.
        /// </summary>
        /// <param name="candidateMethodName">The candidate method name value.</param>
        /// <param name="requestedMethodName">The requested method name value.</param>
        /// <param name="explicitInterfaceTypeNames">The explicit interface type names value.</param>
        /// <param name="useRelaxedNameComparison">The use relaxed name comparison value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool IsForwardTargetMethodNameMatch(
            string candidateMethodName,
            string requestedMethodName,
            IReadOnlyList<string> explicitInterfaceTypeNames,
            bool useRelaxedNameComparison,
            bool useIgnoreCaseMemberMatching)
        {
            var comparison = useIgnoreCaseMemberMatching ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (string.Equals(candidateMethodName, requestedMethodName, comparison))
            {
                return true;
            }

            // Relaxed mode accepts explicit-interface method naming (TypeName.MethodName).
            if (useRelaxedNameComparison &&
                candidateMethodName.EndsWith("." + requestedMethodName, StringComparison.Ordinal))
            {
                return true;
            }

            for (var i = 0; i < explicitInterfaceTypeNames.Count; i++)
            {
                var explicitInterfaceTypeName = explicitInterfaceTypeNames[i];
                if (StringUtil.IsNullOrWhiteSpace(explicitInterfaceTypeName))
                {
                    continue;
                }

                var normalizedInterfaceTypeName = explicitInterfaceTypeName.Replace("+", ".");
                if (string.Equals(candidateMethodName, $"{normalizedInterfaceTypeName}.{requestedMethodName}", comparison))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Attempts to get forward explicit interface type names.
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="explicitInterfaceTypeNames">The explicit interface type names value.</param>
        /// <param name="useRelaxedNameComparison">The use relaxed name comparison value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetForwardExplicitInterfaceTypeNames(
            MethodDef proxyMethod,
            out IReadOnlyList<string> explicitInterfaceTypeNames,
            out bool useRelaxedNameComparison)
        {
            var explicitInterfaceTypeNamesList = new List<string>();
            var useRelaxed = false;
            explicitInterfaceTypeNames = explicitInterfaceTypeNamesList;

            AddFrom(proxyMethod.CustomAttributes);
            if (TryGetDeclaringProperty(proxyMethod, out var declaringProperty))
            {
                AddFrom(declaringProperty!.CustomAttributes);
            }

            useRelaxedNameComparison = useRelaxed;
            return useRelaxed || explicitInterfaceTypeNamesList.Count > 0;

            void AddFrom(IList<CustomAttribute> customAttributes)
            {
                foreach (var customAttribute in customAttributes)
                {
                    if (!IsDuckAttribute(customAttribute))
                    {
                        continue;
                    }

                    foreach (var namedArgument in customAttribute.NamedArguments)
                    {
                        if (!string.Equals(namedArgument.Name.String, "ExplicitInterfaceTypeName", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        if (!TryGetStringArgument(namedArgument.Argument.Value, out var configuredName))
                        {
                            continue;
                        }

                        // Dynamic duck typing treats ExplicitInterfaceTypeName as a single interface name (no comma
                        // list, no trimming) and only an exact "*" as the wildcard, so mirror that for parity.
                        if (string.Equals(configuredName, "*", StringComparison.Ordinal))
                        {
                            useRelaxed = true;
                        }
                        else
                        {
                            explicitInterfaceTypeNamesList.Add(configuredName!);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to get forward parameter type names configured via [Duck(ParameterTypeNames=...)].
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="parameterTypeNames">The parameter type names value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetForwardParameterTypeNames(MethodDef proxyMethod, out IReadOnlyList<string> parameterTypeNames)
        {
            var names = new List<string>();
            parameterTypeNames = names;

            if (TryGetForwardParameterTypeNamesFromAttributes(proxyMethod.CustomAttributes, out parameterTypeNames))
            {
                return true;
            }

            if (TryGetDeclaringProperty(proxyMethod, out var declaringProperty) &&
                TryGetForwardParameterTypeNamesFromAttributes(declaringProperty!.CustomAttributes, out parameterTypeNames))
            {
                return true;
            }

            parameterTypeNames = names;
            return false;
        }

        /// <summary>
        /// Attempts to get forward parameter type names from custom attributes.
        /// </summary>
        /// <param name="customAttributes">The custom attributes value.</param>
        /// <param name="parameterTypeNames">The parameter type names value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetForwardParameterTypeNamesFromAttributes(IList<CustomAttribute> customAttributes, out IReadOnlyList<string> parameterTypeNames)
        {
            foreach (var customAttribute in customAttributes)
            {
                if (!IsDuckAttribute(customAttribute))
                {
                    continue;
                }

                if (TryGetDuckAttributeParameterTypeNames(customAttribute, out parameterTypeNames))
                {
                    return true;
                }
            }

            parameterTypeNames = Array.Empty<string>();
            return false;
        }

        /// <summary>
        /// Attempts to build a forward method binding plan between proxy and target methods.
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="targetMethod">The target method value.</param>
        /// <param name="closedGenericMethodArguments">The closed generic method arguments value.</param>
        /// <param name="enforceMethodSelectionRules">
        /// true when the target method is a candidate of the metadata scan: its parameters must also satisfy the rules dynamic
        /// duck typing uses to select a method; false when dynamic duck typing selected it (Type.GetMethod and its default binder
        /// accept boxing and enum conversions): only a conversion dynamic duck typing can emit is required.
        /// </param>
        /// <param name="binding">The binding value.</param>
        /// <param name="failure">The failure value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryCreateForwardMethodBinding(
            MethodDef proxyMethod,
            MethodDef targetMethod,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericMethodArguments,
            bool isReverseMapping,
            bool enforceMethodSelectionRules,
            out ForwardMethodBindingInfo binding,
            out MethodCompatibilityFailure? failure)
        {
            var phaseStopwatch = StartProfilePhase();
            try
            {
                var proxyMethodPlan = GetOrCreateProxyMethodPlan(proxyMethod);
                var targetMethodPlan = GetOrCreateMethodPlan(targetMethod);
                var cacheKeyStopwatch = StartProfilePhase();
                var cacheKey = BuildForwardMethodBindingPlanCacheKey(
                    proxyMethodPlan,
                    targetMethodPlan,
                    closedGenericProxyTypeArguments,
                    closedGenericTargetTypeArguments,
                    closedGenericMethodArguments,
                    isReverseMapping,
                    enforceMethodSelectionRules);
                StopProfilePhase(cacheKeyStopwatch, seconds => _currentProfile!.ForwardMethodBindingPlanCacheKeyBuildSeconds += seconds);
                if (_currentExecutionContext?.TryGetForwardMethodBindingPlan(cacheKey, out var cachedPlan) == true)
                {
                    if (_currentProfile is not null)
                    {
                        _currentProfile.ForwardBindingPlanCacheHits++;
                    }

                    if (cachedPlan.Succeeded)
                    {
                        binding = cachedPlan.Binding;
                        failure = null;
                        return true;
                    }

                    binding = default;
                    failure = new MethodCompatibilityFailure(cachedPlan.FailureDetail ?? $"Method binding for '{proxyMethod.FullName}' is not compatible.");
                    return false;
                }

                if (_currentProfile is not null)
                {
                    _currentProfile.ForwardBindingPlanCacheMisses++;
                }

                var proxyParameterCount = proxyMethod.MethodSig.Params.Count;
                var targetParameterCount = targetMethod.MethodSig.Params.Count;
                if (isReverseMapping
                    ? proxyParameterCount != targetParameterCount
                    : !IsParameterCountCompatibleWithProxy(targetMethod, proxyParameterCount, allowTrailingOptionalTargetParameters: true))
                {
                    failure = new MethodCompatibilityFailure(
                        $"Parameter count mismatch between proxy method '{proxyMethod.FullName}' and target method '{targetMethod.FullName}'.");
                    binding = default;
                    _currentExecutionContext?.CacheForwardMethodBindingPlan(cacheKey, new ForwardMethodBindingPlanCacheEntry(failure.Value.Detail));
                    return false;
                }

                if (closedGenericMethodArguments is null)
                {
                    if (proxyMethod.MethodSig.GenParamCount != targetMethod.MethodSig.GenParamCount)
                    {
                        failure = new MethodCompatibilityFailure(
                            $"Generic arity mismatch between proxy method '{proxyMethod.FullName}' and target method '{targetMethod.FullName}'.");
                        binding = default;
                        _currentExecutionContext?.CacheForwardMethodBindingPlan(cacheKey, new ForwardMethodBindingPlanCacheEntry(failure.Value.Detail));
                        return false;
                    }
                }
                else
                {
                    if (proxyMethod.MethodSig.GenParamCount != 0 || targetMethod.MethodSig.GenParamCount != closedGenericMethodArguments.Count)
                    {
                        failure = new MethodCompatibilityFailure(
                            $"Generic arity mismatch between proxy method '{proxyMethod.FullName}' and target method '{targetMethod.FullName}'.");
                        binding = default;
                        _currentExecutionContext?.CacheForwardMethodBindingPlan(cacheKey, new ForwardMethodBindingPlanCacheEntry(failure.Value.Detail));
                        return false;
                    }
                }

                var parameterBindings = new MethodParameterBinding[proxyParameterCount];
                for (var parameterIndex = 0; parameterIndex < proxyParameterCount; parameterIndex++)
                {
                    if (!TryCreateForwardMethodParameterBinding(proxyMethod, targetMethod, closedGenericProxyTypeArguments, closedGenericTargetTypeArguments, closedGenericMethodArguments, parameterIndex, isReverseMapping, enforceMethodSelectionRules, out var parameterBinding, out failure))
                    {
                        binding = default;
                        _currentExecutionContext?.CacheForwardMethodBindingPlan(cacheKey, new ForwardMethodBindingPlanCacheEntry(failure?.Detail ?? $"Method binding for '{proxyMethod.FullName}' is not compatible."));
                        return false;
                    }

                    parameterBindings[parameterIndex] = parameterBinding;
                }

                var proxyReturnType = SubstituteTypeAndMethodGenericTypeArguments(proxyMethod.MethodSig.RetType, closedGenericProxyTypeArguments, closedGenericMethodArguments: null);
                var targetReturnType = SubstituteTypeAndMethodGenericTypeArguments(targetMethod.MethodSig.RetType, closedGenericTargetTypeArguments, closedGenericMethodArguments);
                if (!TryCreateReturnConversion(proxyReturnType, targetReturnType, isReverseMapping, out var returnConversion))
                {
                    failure = new MethodCompatibilityFailure(
                        $"Return type mismatch between proxy method '{proxyMethod.FullName}' and target method '{targetMethod.FullName}'.");
                    binding = default;
                    _currentExecutionContext?.CacheForwardMethodBindingPlan(cacheKey, new ForwardMethodBindingPlanCacheEntry(failure.Value.Detail));
                    return false;
                }

                // A reverse proxy's property getter extracts the instance of the duck type the delegation's getter returns, where
                // a reverse method creates a reverse proxy of it (DuckType.CreateCache<T>.CreateReverse), like dynamic duck typing.
                if (isReverseMapping &&
                    returnConversion.Kind == MethodReturnConversionKind.DuckChainToProxy &&
                    proxyMethod.IsSpecialName &&
                    proxyMethod.Name.StartsWith("get_", StringComparison.Ordinal) &&
                    FindPropertyFromAccessor(proxyMethod) is not null)
                {
                    returnConversion = MethodReturnConversion.ExtractDuckTypeInstance(targetReturnType, proxyReturnType, keepsNull: false);
                }

                binding = new ForwardMethodBindingInfo(parameterBindings, returnConversion, closedGenericMethodArguments, targetParameterCount - proxyParameterCount);
                failure = null;
                _currentExecutionContext?.CacheForwardMethodBindingPlan(cacheKey, new ForwardMethodBindingPlanCacheEntry(binding));
                return true;
            }
            finally
            {
                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.ForwardMethodBindingSeconds += seconds);
            }
        }

        private static bool IsParameterCountCompatibleWithProxy(MethodDef targetMethod, int proxyParameterCount, bool allowTrailingOptionalTargetParameters)
        {
            var targetParameterCount = targetMethod.MethodSig.Params.Count;
            if (targetParameterCount == proxyParameterCount)
            {
                return true;
            }

            if (!allowTrailingOptionalTargetParameters || targetParameterCount < proxyParameterCount)
            {
                return false;
            }

            return AreTrailingTargetParametersOptional(targetMethod, proxyParameterCount);
        }

        private static bool AreTrailingTargetParametersOptional(MethodDef targetMethod, int proxyParameterCount)
        {
            for (var parameterIndex = proxyParameterCount; parameterIndex < targetMethod.MethodSig.Params.Count; parameterIndex++)
            {
                if (!IsTargetParameterOptional(targetMethod, parameterIndex))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsTargetParameterOptional(MethodDef targetMethod, int parameterIndex)
        {
            foreach (var parameter in targetMethod.Parameters)
            {
                if (parameter.MethodSigIndex != parameterIndex)
                {
                    continue;
                }

                var paramDef = parameter.ParamDef;
                return paramDef?.IsOptional == true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to build a forward parameter binding plan for a single method parameter.
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="targetMethod">The target method value.</param>
        /// <param name="closedGenericMethodArguments">The closed generic method arguments value.</param>
        /// <param name="parameterIndex">The parameter index value.</param>
        /// <param name="parameterBinding">The parameter binding value.</param>
        /// <param name="failure">The failure value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryCreateForwardMethodParameterBinding(
            MethodDef proxyMethod,
            MethodDef targetMethod,
            IReadOnlyList<TypeSig>? closedGenericProxyTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericTargetTypeArguments,
            IReadOnlyList<TypeSig>? closedGenericMethodArguments,
            int parameterIndex,
            bool isReverseMapping,
            bool enforceMethodSelectionRules,
            out MethodParameterBinding parameterBinding,
            out MethodCompatibilityFailure? failure)
        {
            var phaseStopwatch = StartProfilePhase();
            try
            {
                var proxyMethodPlan = GetOrCreateProxyMethodPlan(proxyMethod);
                var targetMethodPlan = GetOrCreateMethodPlan(targetMethod);
                var proxyParameterType = SubstituteTypeAndMethodGenericTypeArguments(proxyMethod.MethodSig.Params[parameterIndex], closedGenericProxyTypeArguments, closedGenericMethodArguments: null);
                var targetParameterType = SubstituteTypeAndMethodGenericTypeArguments(targetMethod.MethodSig.Params[parameterIndex], closedGenericTargetTypeArguments, closedGenericMethodArguments);

                var proxyIsByRef = proxyParameterType.ElementType == ElementType.ByRef;
                var targetIsByRef = targetParameterType.ElementType == ElementType.ByRef;
                if (proxyIsByRef != targetIsByRef)
                {
                    failure = new MethodCompatibilityFailure(
                        $"By-ref parameter mismatch between proxy method '{proxyMethod.FullName}' and target method '{targetMethod.FullName}'.");
                    parameterBinding = default;
                    return false;
                }

                if (!proxyIsByRef)
                {
                    if (!TryCreateMethodArgumentConversion(proxyParameterType, targetParameterType, isReverseMapping, enforceMethodSelectionRules, out var argumentConversion))
                    {
                        failure = new MethodCompatibilityFailure(
                            $"Parameter type mismatch between proxy method '{proxyMethod.FullName}' and target method '{targetMethod.FullName}'.");
                        parameterBinding = default;
                        return false;
                    }

                    parameterBinding = MethodParameterBinding.ForStandard(proxyParameterType, targetParameterType, argumentConversion);
                    failure = null;
                    return true;
                }

                _ = proxyMethodPlan.TryGetParameterDirection(parameterIndex, out var proxyParameterDirection);
                _ = targetMethodPlan.TryGetParameterDirection(parameterIndex, out var targetParameterDirection);
                var proxyIsOut = proxyParameterDirection.IsOut;
                var targetIsOut = targetParameterDirection.IsOut;
                if (proxyIsOut != targetIsOut)
                {
                    failure = new MethodCompatibilityFailure(
                        $"Parameter direction mismatch between proxy method '{proxyMethod.FullName}' and target method '{targetMethod.FullName}'.");
                    parameterBinding = default;
                    return false;
                }

                // Both proxy and target parameters must expose by-ref element types for by-ref adaptation.
                if (!TryGetByRefElementType(proxyParameterType, out var proxyByRefElementTypeSig) ||
                    !TryGetByRefElementType(targetParameterType, out var targetByRefElementTypeSig))
                {
                    failure = new MethodCompatibilityFailure(
                        $"By-ref parameter mismatch between proxy method '{proxyMethod.FullName}' and target method '{targetMethod.FullName}'.");
                    parameterBinding = default;
                    return false;
                }

                if (enforceMethodSelectionRules &&
                    !MatchesByRefDynamicMethodParameterSelectionRule(proxyByRefElementTypeSig!, targetByRefElementTypeSig!, isReverseMapping))
                {
                    failure = new MethodCompatibilityFailure(
                        $"Parameter type mismatch between proxy method '{proxyMethod.FullName}' and target method '{targetMethod.FullName}'.");
                    parameterBinding = default;
                    return false;
                }

                if (AreTypesEquivalent(proxyParameterType, targetParameterType))
                {
                    parameterBinding = MethodParameterBinding.ForByRefDirect(
                        proxyParameterType,
                        targetParameterType,
                        proxyByRefElementTypeSig!,
                        targetByRefElementTypeSig!,
                        proxyIsOut);
                    failure = null;
                    return true;
                }

                MethodArgumentConversion preCallConversion;
                if (proxyIsOut)
                {
                    preCallConversion = MethodArgumentConversion.None();
                }
                else if (!TryCreateMethodArgumentConversion(proxyByRefElementTypeSig!, targetByRefElementTypeSig!, isReverseMapping, enforceMethodSelectionRules, out preCallConversion))
                {
                    failure = new MethodCompatibilityFailure(
                        $"Parameter type mismatch between proxy method '{proxyMethod.FullName}' and target method '{targetMethod.FullName}'.");
                    parameterBinding = default;
                    return false;
                }

                if (!TryCreateByRefPostCallConversion(proxyByRefElementTypeSig!, targetByRefElementTypeSig!, isReverseMapping, out var postCallConversion))
                {
                    failure = new MethodCompatibilityFailure(
                        $"By-ref parameter mismatch between proxy method '{proxyMethod.FullName}' and target method '{targetMethod.FullName}'.");
                    parameterBinding = default;
                    return false;
                }

                parameterBinding = MethodParameterBinding.ForByRefWithLocal(
                    proxyParameterType,
                    targetParameterType,
                    proxyByRefElementTypeSig!,
                    targetByRefElementTypeSig!,
                    proxyIsOut,
                    preCallConversion,
                    postCallConversion);
                failure = null;
                return true;
            }
            finally
            {
                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.ForwardParameterBindingSeconds += seconds);
            }
        }

        /// <summary>
        /// Attempts to select an argument-conversion strategy from proxy parameter type to target parameter type.
        /// </summary>
        /// <param name="proxyParameterType">The proxy parameter type value.</param>
        /// <param name="targetParameterType">The target parameter type value.</param>
        /// <param name="argumentConversion">The argument conversion value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryCreateMethodArgumentConversion(
            TypeSig proxyParameterType,
            TypeSig targetParameterType,
            bool isReverseMapping,
            bool enforceMethodSelectionRules,
            out MethodArgumentConversion argumentConversion)
        {
            var cacheKeyStopwatch = StartProfilePhase();
            var conversionCacheKey = BuildMethodArgumentConversionCacheKey(proxyParameterType, targetParameterType, isReverseMapping, enforceMethodSelectionRules);
            StopProfilePhase(cacheKeyStopwatch, seconds => _currentProfile!.MethodArgumentConversionCacheKeyBuildSeconds += seconds);
            if (_currentExecutionContext?.TryGetMethodArgumentConversion(conversionCacheKey, out var cachedConversion) == true)
            {
                if (_currentProfile is not null)
                {
                    _currentProfile.ConversionPlanCacheHits++;
                }

                if (cachedConversion.Succeeded)
                {
                    argumentConversion = cachedConversion.Conversion;
                    return true;
                }

                argumentConversion = default;
                return false;
            }

            if (_currentProfile is not null)
            {
                _currentProfile.ConversionPlanCacheMisses++;
            }

            // Dynamic duck typing compares a parameter type built on a generic parameter of the proxy method (e.g. IBox<T>) with
            // the parameter type of the target method definition, built on the target method's own generic parameter: they're
            // never the same type, nor assignable, so it duck chains the argument, unless the proxy type is a value type (not
            // [DuckCopy]) or a type of the core library.
            if (!isReverseMapping && IsBuiltOnMethodGenericParameter(proxyParameterType) && IsDuckChainedByDynamicDuckTyping(proxyParameterType))
            {
                argumentConversion = MethodArgumentConversion.ExtractDuckTypeInstance(proxyParameterType, targetParameterType);
                _currentExecutionContext?.CacheMethodArgumentConversion(conversionCacheKey, new MethodArgumentConversionCacheEntry(argumentConversion));
                return true;
            }

            if (AreTypesEquivalent(proxyParameterType, targetParameterType))
            {
                argumentConversion = MethodArgumentConversion.None();
                _currentExecutionContext?.CacheMethodArgumentConversion(conversionCacheKey, new MethodArgumentConversionCacheEntry(argumentConversion));
                return true;
            }

            if (TryGetValueWithTypeArgument(proxyParameterType, out var proxyValueWithTypeArgument))
            {
                // ValueWithType<T>: unwrap first, then reuse normal argument conversion rules for T -> target.
                if (!TryCreateMethodArgumentConversion(proxyValueWithTypeArgument!, targetParameterType, isReverseMapping, enforceMethodSelectionRules, out argumentConversion))
                {
                    argumentConversion = default;
                    _currentExecutionContext?.CacheMethodArgumentConversion(conversionCacheKey, new MethodArgumentConversionCacheEntry());
                    return false;
                }

                argumentConversion = argumentConversion.WithValueWithTypeUnwrap(proxyParameterType, proxyValueWithTypeArgument!);
                _currentExecutionContext?.CacheMethodArgumentConversion(conversionCacheKey, new MethodArgumentConversionCacheEntry(argumentConversion));
                return true;
            }

            var requiresDuckChaining = isReverseMapping
                                           ? IsDuckChainingRequired(proxyParameterType, targetParameterType)
                                           : IsDuckChainingRequired(targetParameterType, proxyParameterType);
            if (requiresDuckChaining)
            {
                // Like dynamic duck typing, a reverse proxy creates a proxy of the argument for the delegation, and a forward proxy
                // extracts the instance of the duck type it receives.
                argumentConversion = isReverseMapping
                                         ? MethodArgumentConversion.DuckChainToProxy(targetParameterType, proxyParameterType)
                                         : MethodArgumentConversion.ExtractDuckTypeInstance(proxyParameterType, targetParameterType);

                _currentExecutionContext?.CacheMethodArgumentConversion(conversionCacheKey, new MethodArgumentConversionCacheEntry(argumentConversion));
                return true;
            }

            if (enforceMethodSelectionRules &&
                !(isReverseMapping
                      ? MatchesDynamicMethodParameterSelectionRule(targetParameterType, proxyParameterType)
                      : MatchesDynamicMethodParameterSelectionRule(proxyParameterType, targetParameterType)))
            {
                argumentConversion = default;
                _currentExecutionContext?.CacheMethodArgumentConversion(conversionCacheKey, new MethodArgumentConversionCacheEntry());
                return false;
            }

            if (CanUseTypeConversion(proxyParameterType, targetParameterType))
            {
                argumentConversion = MethodArgumentConversion.TypeConversion(proxyParameterType, targetParameterType);
                _currentExecutionContext?.CacheMethodArgumentConversion(conversionCacheKey, new MethodArgumentConversionCacheEntry(argumentConversion));
                return true;
            }

            argumentConversion = default;
            _currentExecutionContext?.CacheMethodArgumentConversion(conversionCacheKey, new MethodArgumentConversionCacheEntry());
            return false;
        }

        /// <summary>
        /// Determines whether proxy/target parameter types satisfy dynamic method-candidate selection rules.
        /// </summary>
        /// <param name="proxyParameterType">The proxy parameter type value.</param>
        /// <param name="targetParameterType">The target parameter type value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool MatchesDynamicMethodParameterSelectionRule(TypeSig proxyParameterType, TypeSig targetParameterType)
        {
            var proxyRuntimeType = TryResolveRuntimeType(proxyParameterType);
            var targetRuntimeType = TryResolveRuntimeType(targetParameterType);

            if (targetRuntimeType is not null && targetRuntimeType.IsGenericParameter)
            {
                return true;
            }

            // Dynamic selector requires exact matches for non-enum value-type parameters.
            if (proxyRuntimeType is not null && targetRuntimeType is not null)
            {
                if (proxyRuntimeType.IsValueType && !proxyRuntimeType.IsEnum)
                {
                    return proxyRuntimeType == targetRuntimeType;
                }

                // For concrete reference types (except object), dynamic selector requires assignability to the target parameter.
                if (proxyRuntimeType.IsClass &&
                    !proxyRuntimeType.IsAbstract &&
                    proxyRuntimeType != typeof(object))
                {
                    return MatchesDynamicConcreteClassParameterSelectionRule(proxyRuntimeType, targetRuntimeType);
                }

                // Interfaces/abstract/object remain eligible for duck-chaining/type adaptation.
                return true;
            }

            return MatchesDynamicMethodParameterSelectionRuleFromMetadata(proxyParameterType, targetParameterType, proxyRuntimeType, targetRuntimeType);
        }

        /// <summary>
        /// Determines whether by-ref element types satisfy dynamic method-candidate selection rules.
        /// </summary>
        /// <param name="proxyParameterElementType">The proxy parameter element type value.</param>
        /// <param name="targetParameterElementType">The target parameter element type value.</param>
        /// <param name="isReverseMapping">The is reverse mapping value.</param>
        /// <returns>true when the by-ref candidate should remain eligible; otherwise, false.</returns>
        private static bool MatchesByRefDynamicMethodParameterSelectionRule(
            TypeSig proxyParameterElementType,
            TypeSig targetParameterElementType,
            bool isReverseMapping)
        {
            return isReverseMapping
                       ? MatchesDynamicMethodParameterSelectionRule(targetParameterElementType, proxyParameterElementType)
                       : MatchesDynamicMethodParameterSelectionRule(proxyParameterElementType, targetParameterElementType);
        }

        /// <summary>
        /// Determines whether a concrete class proxy parameter matches dynamic selection rules,
        /// including generic-argument compatibility fallbacks used by the runtime resolver.
        /// </summary>
        /// <param name="proxyRuntimeType">The proxy runtime type value.</param>
        /// <param name="targetRuntimeType">The target runtime type value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool MatchesDynamicConcreteClassParameterSelectionRule(Type proxyRuntimeType, Type targetRuntimeType)
        {
            if (targetRuntimeType.IsAssignableFrom(proxyRuntimeType))
            {
                return true;
            }

            if (!targetRuntimeType.IsGenericType || !proxyRuntimeType.IsGenericType)
            {
                return false;
            }

            if (targetRuntimeType.ToString() == proxyRuntimeType.ToString())
            {
                return true;
            }

            var targetGenericArguments = targetRuntimeType.GenericTypeArguments;
            var proxyGenericArguments = proxyRuntimeType.GenericTypeArguments;
            if (targetGenericArguments.Length != proxyGenericArguments.Length)
            {
                return false;
            }

            for (var i = 0; i < targetGenericArguments.Length; i++)
            {
                var targetGenericArgument = targetGenericArguments[i];
                var proxyGenericArgument = proxyGenericArguments[i];

                if (targetGenericArgument.IsByRef != proxyGenericArgument.IsByRef)
                {
                    return false;
                }

                if (targetGenericArgument.IsByRef)
                {
                    targetGenericArgument = targetGenericArgument.GetElementType()!;
                    proxyGenericArgument = proxyGenericArgument.GetElementType()!;
                }

                if (targetGenericArgument.IsGenericParameter)
                {
                    continue;
                }

                if (proxyGenericArgument.IsValueType &&
                    !proxyGenericArgument.IsEnum &&
                    proxyGenericArgument != targetGenericArgument)
                {
                    return false;
                }

                if (proxyGenericArgument.IsClass &&
                    !proxyGenericArgument.IsAbstract &&
                    proxyGenericArgument != typeof(object) &&
                    !targetGenericArgument.IsAssignableFrom(proxyGenericArgument))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Applies dynamic-equivalent method-selection rules when runtime type resolution is incomplete.
        /// </summary>
        /// <param name="proxyParameterType">The proxy parameter type value.</param>
        /// <param name="targetParameterType">The target parameter type value.</param>
        /// <param name="proxyRuntimeType">The proxy runtime type value.</param>
        /// <param name="targetRuntimeType">The target runtime type value.</param>
        /// <returns>true when metadata-only rules permit the candidate; otherwise, false.</returns>
        private static bool MatchesDynamicMethodParameterSelectionRuleFromMetadata(
            TypeSig proxyParameterType,
            TypeSig targetParameterType,
            Type? proxyRuntimeType,
            Type? targetRuntimeType)
        {
            if (targetParameterType.IsGenericParameter || targetRuntimeType?.IsGenericParameter == true)
            {
                return true;
            }

            if (IsNonEnumValueTypeForMethodSelection(proxyParameterType, proxyRuntimeType))
            {
                return AreTypesEquivalent(proxyParameterType, targetParameterType);
            }

            if (!IsConcreteClassForMethodSelection(proxyParameterType, proxyRuntimeType))
            {
                return true;
            }

            return MatchesDynamicConcreteClassParameterSelectionRuleFromMetadata(
                proxyParameterType,
                targetParameterType,
                proxyRuntimeType,
                targetRuntimeType);
        }

        /// <summary>
        /// Applies dynamic-equivalent concrete-class parameter selection when runtime resolution is incomplete.
        /// </summary>
        /// <param name="proxyParameterType">The proxy parameter type value.</param>
        /// <param name="targetParameterType">The target parameter type value.</param>
        /// <param name="proxyRuntimeType">The proxy runtime type value.</param>
        /// <param name="targetRuntimeType">The target runtime type value.</param>
        /// <returns>true when the candidate should remain eligible; otherwise, false.</returns>
        private static bool MatchesDynamicConcreteClassParameterSelectionRuleFromMetadata(
            TypeSig proxyParameterType,
            TypeSig targetParameterType,
            Type? proxyRuntimeType,
            Type? targetRuntimeType)
        {
            if (proxyRuntimeType is not null && targetRuntimeType is not null)
            {
                return MatchesDynamicConcreteClassParameterSelectionRule(proxyRuntimeType, targetRuntimeType);
            }

            if (IsObjectTypeSig(targetParameterType))
            {
                return true;
            }

            if (IsTypeAssignableFrom(targetParameterType, proxyParameterType))
            {
                return true;
            }

            if (AreTypesEquivalent(proxyParameterType, targetParameterType))
            {
                return true;
            }

            if (proxyParameterType is not GenericInstSig proxyGenericInst ||
                targetParameterType is not GenericInstSig targetGenericInst)
            {
                return false;
            }

            if (string.Equals(targetGenericInst.ToString(), proxyGenericInst.ToString(), StringComparison.Ordinal))
            {
                return true;
            }

            if (targetGenericInst.GenericArguments.Count != proxyGenericInst.GenericArguments.Count)
            {
                return false;
            }

            for (var i = 0; i < targetGenericInst.GenericArguments.Count; i++)
            {
                var targetGenericArgument = targetGenericInst.GenericArguments[i];
                var proxyGenericArgument = proxyGenericInst.GenericArguments[i];

                if (targetGenericArgument.ElementType == ElementType.ByRef || proxyGenericArgument.ElementType == ElementType.ByRef)
                {
                    if (!TryGetByRefElementType(targetGenericArgument, out var targetByRefElementType) ||
                        !TryGetByRefElementType(proxyGenericArgument, out var proxyByRefElementType))
                    {
                        return false;
                    }

                    targetGenericArgument = targetByRefElementType!;
                    proxyGenericArgument = proxyByRefElementType!;
                }

                if (targetGenericArgument.IsGenericParameter)
                {
                    continue;
                }

                var proxyGenericArgumentRuntimeType = TryResolveRuntimeType(proxyGenericArgument);
                if (IsNonEnumValueTypeForMethodSelection(proxyGenericArgument, proxyGenericArgumentRuntimeType) &&
                    !AreTypesEquivalent(proxyGenericArgument, targetGenericArgument))
                {
                    return false;
                }

                if (IsConcreteClassForMethodSelection(proxyGenericArgument, proxyGenericArgumentRuntimeType) &&
                    !IsTypeAssignableFrom(targetGenericArgument, proxyGenericArgument) &&
                    !AreTypesEquivalent(proxyGenericArgument, targetGenericArgument))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether parameter type should be treated as a non-enum value type for method-selection parity.
        /// </summary>
        /// <param name="parameterType">The parameter type value.</param>
        /// <param name="runtimeType">The runtime type value.</param>
        /// <returns>true when the parameter is a non-enum value type; otherwise, false.</returns>
        private static bool IsNonEnumValueTypeForMethodSelection(TypeSig parameterType, Type? runtimeType)
        {
            if (runtimeType is not null)
            {
                return runtimeType.IsValueType && !runtimeType.IsEnum;
            }

            if (!parameterType.IsValueType)
            {
                return false;
            }

            var parameterTypeDef = parameterType.ToTypeDefOrRef()?.ResolveTypeDef();
            // Only enforce strict non-enum value-type matching when metadata can prove enum status.
            // Unknown metadata should stay permissive to match dynamic resolver behavior.
            return parameterTypeDef is not null && parameterTypeDef.IsValueType && !parameterTypeDef.IsEnum;
        }

        /// <summary>
        /// Determines whether parameter type should be treated as a concrete class for method-selection parity.
        /// </summary>
        /// <param name="parameterType">The parameter type value.</param>
        /// <param name="runtimeType">The runtime type value.</param>
        /// <returns>true when the parameter is a non-abstract concrete class excluding object; otherwise, false.</returns>
        private static bool IsConcreteClassForMethodSelection(TypeSig parameterType, Type? runtimeType)
        {
            if (runtimeType is not null)
            {
                return runtimeType.IsClass &&
                       !runtimeType.IsAbstract &&
                       runtimeType != typeof(object);
            }

            if (IsObjectTypeSig(parameterType))
            {
                return false;
            }

            if (parameterType.ElementType == ElementType.String)
            {
                return true;
            }

            var parameterTypeDef = parameterType.ToTypeDefOrRef()?.ResolveTypeDef();
            return parameterTypeDef?.IsClass == true && !parameterTypeDef.IsAbstract;
        }

        /// <summary>
        /// Attempts to select post-call conversion for by-ref argument write-back.
        /// </summary>
        /// <param name="proxyParameterElementType">The proxy parameter element type value.</param>
        /// <param name="targetParameterElementType">The target parameter element type value.</param>
        /// <param name="isReverseMapping">The is reverse mapping value.</param>
        /// <param name="returnConversion">The return conversion value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryCreateByRefPostCallConversion(TypeSig proxyParameterElementType, TypeSig targetParameterElementType, bool isReverseMapping, out MethodReturnConversion returnConversion)
        {
            var conversionCacheKey = BuildMethodReturnConversionCacheKey(proxyParameterElementType, targetParameterElementType, isReverseMapping, discriminator: "byref");
            if (_currentExecutionContext?.TryGetMethodReturnConversion(conversionCacheKey, out var cachedConversion) == true)
            {
                if (_currentProfile is not null)
                {
                    _currentProfile.ConversionPlanCacheHits++;
                }

                if (cachedConversion.Succeeded)
                {
                    returnConversion = cachedConversion.Conversion;
                    return true;
                }

                returnConversion = default;
                return false;
            }

            if (_currentProfile is not null)
            {
                _currentProfile.ConversionPlanCacheMisses++;
            }

            if (isReverseMapping)
            {
                if (AreTypesEquivalent(proxyParameterElementType, targetParameterElementType))
                {
                    returnConversion = MethodReturnConversion.None();
                    _currentExecutionContext?.CacheMethodReturnConversion(conversionCacheKey, new MethodReturnConversionCacheEntry(returnConversion));
                    return true;
                }

                // Like dynamic duck typing, the value the delegation writes is a duck type whose instance is extracted (a null one
                // throws a NullReferenceException).
                if (IsDuckChainingRequired(proxyParameterElementType, targetParameterElementType))
                {
                    returnConversion = MethodReturnConversion.ExtractDuckTypeInstance(targetParameterElementType, proxyParameterElementType, keepsNull: false);
                    _currentExecutionContext?.CacheMethodReturnConversion(conversionCacheKey, new MethodReturnConversionCacheEntry(returnConversion));
                    return true;
                }

                if (CanUseTypeConversion(targetParameterElementType, proxyParameterElementType))
                {
                    returnConversion = MethodReturnConversion.TypeConversion(targetParameterElementType, proxyParameterElementType);
                    _currentExecutionContext?.CacheMethodReturnConversion(conversionCacheKey, new MethodReturnConversionCacheEntry(returnConversion));
                    return true;
                }
            }
            else if (TryCreateReturnConversion(proxyParameterElementType, targetParameterElementType, out returnConversion))
            {
                _currentExecutionContext?.CacheMethodReturnConversion(conversionCacheKey, new MethodReturnConversionCacheEntry(returnConversion));
                return true;
            }

            returnConversion = default;
            _currentExecutionContext?.CacheMethodReturnConversion(conversionCacheKey, new MethodReturnConversionCacheEntry());
            return false;
        }

        /// <summary>
        /// Attempts to get method parameter direction.
        /// </summary>
        /// <param name="method">The method value.</param>
        /// <param name="parameterIndex">The parameter index value.</param>
        /// <param name="direction">The direction value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetMethodParameterDirection(MethodDef method, int parameterIndex, out ParameterDirection direction)
        {
            foreach (var parameter in method.Parameters)
            {
                if (parameter.MethodSigIndex != parameterIndex)
                {
                    continue;
                }

                var paramDef = parameter.ParamDef;
                direction = new ParameterDirection(paramDef?.IsOut ?? false, paramDef?.IsIn ?? false);
                return true;
            }

            direction = default;
            return false;
        }

        /// <summary>
        /// Attempts to get value with type argument.
        /// </summary>
        /// <param name="typeSig">The type sig value.</param>
        /// <param name="valueArgument">The value argument value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetValueWithTypeArgument(TypeSig typeSig, out TypeSig? valueArgument)
        {
            valueArgument = null;
            if (typeSig is not GenericInstSig genericInstSig || genericInstSig.GenericArguments.Count != 1)
            {
                return false;
            }

            var genericType = genericInstSig.GenericType?.TypeDefOrRef;
            if (genericType is null)
            {
                return false;
            }

            if (!string.Equals(genericType.FullName, typeof(ValueWithType<>).FullName, StringComparison.Ordinal))
            {
                return false;
            }

            valueArgument = genericInstSig.GenericArguments[0];
            return true;
        }

        /// <summary>
        /// Creates value with type create method ref.
        /// </summary>
        /// <param name="moduleDef">The module def value.</param>
        /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
        /// <param name="innerTypeSig">The inner type sig value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IMethodDefOrRef CreateValueWithTypeCreateMethodRef(ModuleDef moduleDef, TypeSig wrapperTypeSig, TypeSig innerTypeSig)
        {
            var cacheKey = string.Concat(BuildTypeSigCacheKey(wrapperTypeSig), "|", BuildTypeSigCacheKey(innerTypeSig));
            if (_currentExecutionContext?.TryGetValueWithTypeCreateMethodRef(cacheKey, out var cachedMethod) == true)
            {
                return cachedMethod!;
            }

            var importedWrapperTypeSig = ImportTypeSigCached(moduleDef, wrapperTypeSig, $"ValueWithType wrapper method '{wrapperTypeSig.FullName}'");
            if (importedWrapperTypeSig is not GenericInstSig wrapperGenericInst)
            {
                throw new InvalidOperationException($"Expected ValueWithType<T> generic wrapper, but found '{importedWrapperTypeSig.FullName}'.");
            }

            var typeSpec = moduleDef.UpdateRowId(new TypeSpecUser(importedWrapperTypeSig));
            var returnTypeSig = new GenericInstSig(wrapperGenericInst.GenericType, new GenericVar(0));
            var methodSig = MethodSig.CreateStatic(
                returnTypeSig,
                new GenericVar(0),
                moduleDef.CorLibTypes.GetTypeRef("System", "Type").ToTypeSig());
            var methodRef = new MemberRefUser(moduleDef, "Create", methodSig, typeSpec);
            var importedMethod = moduleDef.UpdateRowId(methodRef);
            _currentExecutionContext?.CacheValueWithTypeCreateMethodRef(cacheKey, importedMethod);
            return importedMethod;
        }

        private static bool TryGetOptionalParameterConstant(MethodDef targetMethod, int parameterIndex, out object? constantValue)
        {
            foreach (var parameter in targetMethod.Parameters)
            {
                if (parameter.MethodSigIndex != parameterIndex)
                {
                    continue;
                }

                if (parameter.ParamDef?.Constant is { } constant)
                {
                    constantValue = constant.Value;
                    return true;
                }

                if (parameter.ParamDef is not null &&
                    TryGetOptionalParameterAttributeConstant(parameter.ParamDef, out constantValue))
                {
                    return true;
                }

                break;
            }

            constantValue = null;
            return false;
        }

        private static bool TryGetOptionalParameterAttributeConstant(ParamDef parameter, out object? constantValue)
        {
            foreach (var customAttribute in parameter.CustomAttributes)
            {
                if (string.Equals(customAttribute.TypeFullName, "System.Runtime.CompilerServices.DecimalConstantAttribute", StringComparison.Ordinal) &&
                    TryGetDecimalConstantAttributeValue(customAttribute, out var decimalValue))
                {
                    constantValue = decimalValue;
                    return true;
                }

                if (string.Equals(customAttribute.TypeFullName, "System.Runtime.CompilerServices.DateTimeConstantAttribute", StringComparison.Ordinal) &&
                    TryGetDateTimeConstantAttributeValue(customAttribute, out var dateTimeValue))
                {
                    constantValue = dateTimeValue;
                    return true;
                }
            }

            constantValue = null;
            return false;
        }

        private static bool TryGetDecimalConstantAttributeValue(CustomAttribute customAttribute, out decimal value)
        {
            value = default;
            if (customAttribute.ConstructorArguments.Count != 5 ||
                !TryGetCustomAttributeByte(customAttribute.ConstructorArguments[0], out var scale) ||
                !TryGetCustomAttributeByte(customAttribute.ConstructorArguments[1], out var sign) ||
                !TryGetCustomAttributeInt32Bits(customAttribute.ConstructorArguments[2], out var high) ||
                !TryGetCustomAttributeInt32Bits(customAttribute.ConstructorArguments[3], out var middle) ||
                !TryGetCustomAttributeInt32Bits(customAttribute.ConstructorArguments[4], out var low))
            {
                return false;
            }

            try
            {
                value = new decimal(low, middle, high, sign != 0, scale);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetDateTimeConstantAttributeValue(CustomAttribute customAttribute, out DateTime value)
        {
            value = default;
            if (customAttribute.ConstructorArguments.Count != 1 ||
                !TryGetCustomAttributeInt64(customAttribute.ConstructorArguments[0], out var ticks))
            {
                return false;
            }

            try
            {
                value = new DateTime(ticks);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryConvertConstantToLong(object value, out long result)
        {
            switch (value)
            {
                case sbyte typedValue:
                    result = typedValue;
                    return true;
                case byte typedValue:
                    result = typedValue;
                    return true;
                case short typedValue:
                    result = typedValue;
                    return true;
                case ushort typedValue:
                    result = typedValue;
                    return true;
                case int typedValue:
                    result = typedValue;
                    return true;
                case uint typedValue:
                    result = typedValue;
                    return true;
                case long typedValue:
                    result = typedValue;
                    return true;
                case ulong typedValue when typedValue <= long.MaxValue:
                    result = (long)typedValue;
                    return true;
                default:
                    result = default;
                    return false;
            }
        }

        private static bool TryGetCustomAttributeByte(CAArgument argument, out byte value)
        {
            switch (argument.Value)
            {
                case byte byteValue:
                    value = byteValue;
                    return true;
                case sbyte sbyteValue when sbyteValue >= 0:
                    value = (byte)sbyteValue;
                    return true;
                case short shortValue when shortValue >= byte.MinValue && shortValue <= byte.MaxValue:
                    value = (byte)shortValue;
                    return true;
                case ushort ushortValue when ushortValue <= byte.MaxValue:
                    value = (byte)ushortValue;
                    return true;
                case int intValue when intValue >= byte.MinValue && intValue <= byte.MaxValue:
                    value = (byte)intValue;
                    return true;
                case uint uintValue when uintValue <= byte.MaxValue:
                    value = (byte)uintValue;
                    return true;
                default:
                    value = default;
                    return false;
            }
        }

        private static bool TryGetCustomAttributeInt32Bits(CAArgument argument, out int value)
        {
            switch (argument.Value)
            {
                case int intValue:
                    value = intValue;
                    return true;
                case uint uintValue:
                    value = unchecked((int)uintValue);
                    return true;
                case short shortValue:
                    value = shortValue;
                    return true;
                case ushort ushortValue:
                    value = ushortValue;
                    return true;
                case byte byteValue:
                    value = byteValue;
                    return true;
                case sbyte sbyteValue:
                    value = sbyteValue;
                    return true;
                default:
                    value = default;
                    return false;
            }
        }

        private static bool TryGetCustomAttributeInt64(CAArgument argument, out long value)
        {
            switch (argument.Value)
            {
                case long longValue:
                    value = longValue;
                    return true;
                case ulong ulongValue when ulongValue <= long.MaxValue:
                    value = (long)ulongValue;
                    return true;
                case int intValue:
                    value = intValue;
                    return true;
                case uint uintValue:
                    value = uintValue;
                    return true;
                case short shortValue:
                    value = shortValue;
                    return true;
                case ushort ushortValue:
                    value = ushortValue;
                    return true;
                case byte byteValue:
                    value = byteValue;
                    return true;
                case sbyte sbyteValue:
                    value = sbyteValue;
                    return true;
                default:
                    value = default;
                    return false;
            }
        }

        /// <summary>
        /// Gets forward target field names.
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IReadOnlyList<string> GetForwardTargetFieldNames(MethodDef proxyMethod)
        {
            var fieldNames = new List<string>();
            var visitedNames = new HashSet<string>(StringComparer.Ordinal);
            var hasConfiguredName = false;

            void AddNames(IEnumerable<string> names)
            {
                foreach (var name in names)
                {
                    if (StringUtil.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    if (visitedNames.Add(name))
                    {
                        fieldNames.Add(name);
                    }
                }
            }

            if (TryGetDuckAttributeNames(proxyMethod.CustomAttributes, out var methodAttributeNames))
            {
                AddNames(methodAttributeNames);
                hasConfiguredName = true;
            }

            if (TryGetDeclaringProperty(proxyMethod, out var declaringProperty) && TryGetDuckAttributeNames(declaringProperty!.CustomAttributes, out var propertyAttributeNames))
            {
                AddNames(propertyAttributeNames);
                hasConfiguredName = true;
            }

            if (!hasConfiguredName && TryGetAccessorPropertyName(proxyMethod.Name.String ?? proxyMethod.Name.ToString(), out var propertyName))
            {
                AddNames(new[] { propertyName! });
            }

            return fieldNames;
        }

        /// <summary>
        /// Attempts to get field accessor kind.
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="accessorKind">The accessor kind value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetFieldAccessorKind(MethodDef proxyMethod, out FieldAccessorKind accessorKind)
        {
            accessorKind = default;
            var methodName = proxyMethod.Name.String ?? proxyMethod.Name.ToString();

            if (methodName.StartsWith("get_", StringComparison.Ordinal) && proxyMethod.MethodSig.Params.Count == 0)
            {
                accessorKind = FieldAccessorKind.Getter;
                return true;
            }

            if (methodName.StartsWith("set_", StringComparison.Ordinal) && proxyMethod.MethodSig.Params.Count == 1 && proxyMethod.MethodSig.RetType.ElementType == ElementType.Void)
            {
                accessorKind = FieldAccessorKind.Setter;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether the method represents a property accessor.
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool IsPropertyAccessorMethod(MethodDef proxyMethod)
        {
            var methodName = proxyMethod.Name.String ?? proxyMethod.Name.ToString();
            return TryGetAccessorPropertyName(methodName, out _);
        }

        /// <summary>
        /// Attempts to get accessor property name.
        /// </summary>
        /// <param name="methodName">The method name value.</param>
        /// <param name="propertyName">The property name value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetAccessorPropertyName(string methodName, out string? propertyName)
        {
            propertyName = null;
            if (methodName.StartsWith("get_", StringComparison.Ordinal) || methodName.StartsWith("set_", StringComparison.Ordinal))
            {
                propertyName = methodName.Substring(4);
                return !StringUtil.IsNullOrWhiteSpace(propertyName);
            }

            return false;
        }

        /// <summary>
        /// Gets forward target method names.
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <returns>The result produced by this operation.</returns>
        private static IReadOnlyList<string> GetForwardTargetMethodNames(MethodDef proxyMethod)
        {
            var methodNames = new List<string>();
            var visitedNames = new HashSet<string>(StringComparer.Ordinal);
            var hasConfiguredName = false;

            void AddNames(IEnumerable<string> names)
            {
                foreach (var name in names)
                {
                    if (StringUtil.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    if (visitedNames.Add(name))
                    {
                        methodNames.Add(name);
                    }
                }
            }

            if (TryGetDuckAttributeNames(proxyMethod.CustomAttributes, out var methodAttributeNames))
            {
                if (proxyMethod.IsSpecialName && TryGetAccessorPrefix(proxyMethod.Name, out var accessorPrefix))
                {
                    AddNames(methodAttributeNames.Select(name => $"{accessorPrefix}{name}"));
                }
                else
                {
                    AddNames(methodAttributeNames);
                }

                hasConfiguredName = true;
            }

            if (proxyMethod.IsSpecialName && TryGetDeclaringProperty(proxyMethod, out var declaringProperty) && TryGetDuckAttributeNames(declaringProperty!.CustomAttributes, out var propertyAttributeNames) && TryGetAccessorPrefix(proxyMethod.Name, out var propertyAccessorPrefix))
            {
                AddNames(propertyAttributeNames.Select(name => $"{propertyAccessorPrefix}{name}"));
                hasConfiguredName = true;
            }

            if (!hasConfiguredName)
            {
                AddNames(new[] { proxyMethod.Name.String ?? proxyMethod.Name.ToString() });
            }

            return methodNames;
        }

        /// <summary>
        /// Attempts to resolve forward closed generic method arguments.
        /// </summary>
        /// <param name="targetType">The target type value.</param>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="closedGenericMethodArguments">The closed generic method arguments value.</param>
        /// <param name="failureReason">The failure reason value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryResolveForwardClosedGenericMethodArguments(
            TypeDef targetType,
            ProxyMethodPlan proxyMethodPlan,
            out IReadOnlyList<TypeSig>? closedGenericMethodArguments,
            out string? failureReason)
        {
            var phaseStopwatch = StartProfilePhase();
            if (_currentProfile is not null)
            {
                _currentProfile.ForwardClosedGenericMethodArgumentResolutionCount++;
            }

            try
            {
            closedGenericMethodArguments = null;
            failureReason = null;
            if (!proxyMethodPlan.HasDuckGenericParameterTypeNames)
            {
                return true;
            }

            var resolvedTypeSigs = new List<TypeSig>(proxyMethodPlan.DuckGenericParameterTypeNames.Count);
            foreach (var genericParameterTypeName in proxyMethodPlan.DuckGenericParameterTypeNames)
            {
                if (!TryResolveRuntimeTypeByName(genericParameterTypeName, out var runtimeType))
                {
                    failureReason =
                        $"Generic parameter type '{genericParameterTypeName}' for proxy method '{proxyMethodPlan.Method.FullName}' could not be resolved.";
                    return false;
                }

                resolvedTypeSigs.Add(targetType.Module.Import(runtimeType!).ToTypeSig());
            }

            closedGenericMethodArguments = resolvedTypeSigs;
            return true;
            }
            finally
            {
                StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.ForwardClosedGenericMethodArgumentResolutionSeconds += seconds);
            }
        }

        /// <summary>
        /// Attempts to get duck generic parameter type names.
        /// </summary>
        /// <param name="proxyMethod">The proxy method value.</param>
        /// <param name="genericParameterTypeNames">The generic parameter type names value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetDuckGenericParameterTypeNames(MethodDef proxyMethod, out IReadOnlyList<string> genericParameterTypeNames)
        {
            var names = new List<string>();
            genericParameterTypeNames = names;

            foreach (var duckAttribute in EnumerateDuckAttributes(proxyMethod))
            {
                foreach (var namedArgument in duckAttribute.NamedArguments)
                {
                    if (!string.Equals(namedArgument.Name.String, "GenericParameterTypeNames", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (TryGetStringArrayArgument(namedArgument.Argument.Value, out var argumentNames))
                    {
                        names.AddRange(argumentNames);
                    }
                }
            }

            return names.Count > 0;
        }

        /// <summary>
        /// Determines whether reverse method attribute.
        /// </summary>
        /// <param name="customAttribute">The custom attribute value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool IsReverseMethodAttribute(CustomAttribute customAttribute)
        {
            return string.Equals(customAttribute.TypeFullName, DuckReverseMethodAttributeTypeName, StringComparison.Ordinal);
        }

        /// <summary>
        /// Determines whether reverse candidate match.
        /// </summary>
        /// <param name="proxyMethodName">The proxy method name value.</param>
        /// <param name="proxyParameterTypes">The proxy parameter types value.</param>
        /// <param name="proxyParameterTypeNames">The proxy parameter type names value.</param>
        /// <param name="reverseAttribute">The reverse attribute value.</param>
        /// <param name="targetMethodName">The target method name value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool IsReverseCandidateMatch(
            string proxyMethodName,
            IReadOnlyList<TypeSig> proxyParameterTypes,
            IReadOnlyList<HashSet<string>> proxyParameterTypeNames,
            CustomAttribute reverseAttribute,
            string targetMethodName)
        {
            // Property accessor prefix (get_/set_) must remain consistent between proxy and candidate target method.
            if (TryGetAccessorPrefix(proxyMethodName, out var proxyAccessorPrefix) &&
                TryGetAccessorPrefix(targetMethodName, out var targetAccessorPrefix) &&
                !string.Equals(proxyAccessorPrefix, targetAccessorPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            var mappedNames = new List<string> { targetMethodName };
            if (TryGetDuckAttributeName(reverseAttribute, out var explicitMappedName) &&
                !StringUtil.IsNullOrWhiteSpace(explicitMappedName))
            {
                mappedNames.Clear();
                foreach (var normalizedName in SplitDuckNames(explicitMappedName!))
                {
                    // Keep accessor prefix when mapping reverse accessor methods with explicit renamed member.
                    if (proxyMethodName.StartsWith("get_", StringComparison.Ordinal) ||
                        proxyMethodName.StartsWith("set_", StringComparison.Ordinal))
                    {
                        mappedNames.Add(proxyMethodName.Substring(0, 4) + normalizedName);
                    }
                    else
                    {
                        mappedNames.Add(normalizedName);
                    }
                }
            }

            if (!mappedNames.Any(mappedName => string.Equals(proxyMethodName, mappedName, StringComparison.Ordinal)))
            {
                return false;
            }

            if (!TryGetDuckAttributeParameterTypeNames(reverseAttribute, out var configuredParameterTypeNames))
            {
                return true;
            }

            if (configuredParameterTypeNames.Count != proxyParameterTypeNames.Count)
            {
                return false;
            }

            for (var i = 0; i < configuredParameterTypeNames.Count; i++)
            {
                if (IsTypeGenericParameter(proxyParameterTypes[i]))
                {
                    continue;
                }

                if (!proxyParameterTypeNames[i].Contains(configuredParameterTypeNames[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether a parameter type is an open generic placeholder.
        /// </summary>
        /// <param name="typeSig">The type sig value.</param>
        /// <returns>true when the type represents a generic parameter; otherwise, false.</returns>
        private static bool IsTypeGenericParameter(TypeSig typeSig)
        {
            if (typeSig is ByRefSig byRefSig)
            {
                typeSig = byRefSig.Next;
            }

            return typeSig is GenericVar or GenericMVar;
        }

        /// <summary>
        /// Determines whether a type is built on a generic parameter of a method (e.g. IBox&lt;T&gt; or T[] in a generic method),
        /// without being one.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>true if the type is built on a generic parameter of a method; otherwise, false.</returns>
        private static bool IsBuiltOnMethodGenericParameter(TypeSig type)
        {
            return !type.IsGenericParameter && ContainsMethodGenericParameter(type);

            static bool ContainsMethodGenericParameter(TypeSig? candidate)
                => candidate switch
                {
                    GenericMVar => true,
                    GenericInstSig genericInstSig => genericInstSig.GenericArguments.Any(ContainsMethodGenericParameter),
                    NonLeafSig nonLeafSig => ContainsMethodGenericParameter(nonLeafSig.Next),
                    _ => false,
                };
        }

        /// <summary>
        /// Gets what dynamic duck typing's NeedsDuckChaining decides for a proxy type it finds neither equal nor assignable to the
        /// target type: it duck chains a [DuckCopy] struct, or a reference type that isn't a type of the core library (an array
        /// is of the module of its element type, a generic parameter of the module of its method).
        /// </summary>
        /// <param name="proxyType">The proxy type.</param>
        /// <returns>true if dynamic duck typing duck chains it; otherwise, false.</returns>
        private static bool IsDuckChainedByDynamicDuckTyping(TypeSig proxyType)
        {
            var elementType = proxyType;
            var isArray = false;
            while (elementType is SZArraySig or ArraySig)
            {
                isArray = true;
                elementType = elementType.Next;
            }

            if (elementType is GenericSig)
            {
                return true;
            }

            var definition = (elementType as GenericInstSig)?.GenericType?.TypeDefOrRef ?? elementType.ToTypeDefOrRef();
            if (definition is null)
            {
                return false;
            }

            var definitionType = definition.ResolveTypeDef();
            if (!isArray && definitionType?.CustomAttributes.Any(attribute => string.Equals(attribute.TypeFullName, DuckCopyAttributeTypeName, StringComparison.Ordinal)) == true)
            {
                return true;
            }

            return (isArray || definitionType?.IsValueType != true) && !IsCoreLibraryType(definition, definitionType);

            // Dynamic duck typing compares the module of the runtime type with the core library's: a type a contract references
            // through a reference assembly (e.g. List<T> through System.Collections) is a type of the core library at runtime.
            static bool IsCoreLibraryType(ITypeDefOrRef definition, TypeDef? definitionType)
            {
                if (definitionType?.Module?.Assembly?.Name?.String is { } definitionAssemblyName)
                {
                    return IsCoreLibraryAssemblyName(definitionAssemblyName);
                }

                if (TryResolveRuntimeType(definition.ToTypeSig()) is { } runtimeType)
                {
                    return runtimeType.Module == typeof(string).Module;
                }

                return IsCoreLibraryAssemblyName(DuckTypeAotNameHelpers.NormalizeAssemblyName(definition.DefinitionAssembly?.Name?.String ?? string.Empty)) ||
                       IsFrameworkImplementationType(definition);
            }
        }

        /// <summary>
        /// Determines whether duck chaining required.
        /// </summary>
        /// <param name="targetType">The target type value.</param>
        /// <param name="proxyType">The proxy type value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool IsDuckChainingRequired(TypeSig targetType, TypeSig proxyType)
        {
            // Like dynamic duck typing, a generic parameter isn't duck chained, but a type built on one (e.g. IBox<T> in a generic
            // method) is decided like any other type.
            if (proxyType.IsGenericParameter || targetType.IsGenericParameter)
            {
                return false;
            }

            if (proxyType.ElementType == ElementType.ByRef || targetType.ElementType == ElementType.ByRef)
            {
                return false;
            }

            if (AreTypesEquivalent(proxyType, targetType))
            {
                return false;
            }

            // Same decision as dynamic duck typing whenever the runtime types can be loaded: it doesn't chain CoreLib types
            // or assignable types (including generic instantiations), whatever their shape. The metadata rules below only
            // approximate it for types the generator can't load.
            if (TryResolveRuntimeType(proxyType) is { } proxyRuntimeType &&
                TryResolveRuntimeType(targetType) is { } targetRuntimeType &&
                AreDuckCopyAttributesBoundToGenerator(proxyRuntimeType))
            {
                return DuckType.NeedsDuckChainingForAot(targetRuntimeType, proxyRuntimeType);
            }

            if (!TryGetDuckChainingProxyType(proxyType, out var proxyTypeForCache))
            {
                return false;
            }

            var proxyTypeDefOrRef = proxyTypeForCache.ToTypeDefOrRef();
            var targetTypeDefOrRef = targetType.ToTypeDefOrRef();
            if (proxyTypeDefOrRef is null || targetTypeDefOrRef is null)
            {
                return false;
            }

            if (IsAssignableFrom(proxyTypeDefOrRef, targetTypeDefOrRef))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Determines whether dynamic duck typing reads the [DuckCopy] attributes of a runtime proxy type (and of the type
        /// wrapped by Nullable) by identity, i.e. they come from the Datadog.Trace the generator runs.
        /// </summary>
        /// <param name="proxyRuntimeType">The runtime proxy type.</param>
        /// <returns>true if its attributes are bound to the generator's Datadog.Trace; otherwise, false.</returns>
        private static bool AreDuckCopyAttributesBoundToGenerator(Type proxyRuntimeType)
        {
            bool IsBound(Type type) => type.CustomAttributes.All(attribute => IsReadByGeneratorDuckTyping(attribute.AttributeType));

            try
            {
                return IsBound(proxyRuntimeType) && (Nullable.GetUnderlyingType(proxyRuntimeType) is not { } underlyingType || IsBound(underlyingType));
            }
            catch (Exception)
            {
                // Attributes that can't be loaded: use the metadata rules.
                return false;
            }
        }

        /// <summary>
        /// Attempts to get duck chaining proxy type.
        /// </summary>
        /// <param name="proxyType">The proxy type value.</param>
        /// <param name="proxyTypeForCache">The proxy type for cache value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool TryGetDuckChainingProxyType(TypeSig proxyType, out TypeSig proxyTypeForCache)
        {
            if (TryGetNullableElementType(proxyType, out var nullableInnerType) && IsDuckProxyCandidate(nullableInnerType!))
            {
                proxyTypeForCache = nullableInnerType!;
                return true;
            }

            if (IsDuckProxyCandidate(proxyType))
            {
                proxyTypeForCache = proxyType;
                return true;
            }

            proxyTypeForCache = null!;
            return false;
        }

        /// <summary>
        /// Determines whether duck proxy candidate.
        /// </summary>
        /// <param name="typeSig">The type sig value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool IsDuckProxyCandidate(TypeSig typeSig)
        {
            var typeDefOrRef = typeSig.ToTypeDefOrRef();
            if (typeDefOrRef is null)
            {
                return false;
            }

            var typeDef = typeDefOrRef.ResolveTypeDef();
            if (typeDef is null)
            {
                return false;
            }

            if (typeDef.IsEnum)
            {
                return false;
            }

            // Like dynamic duck typing, CoreLib types are never duck chained.
            if (typeDef.IsInterface || typeDef.IsClass)
            {
                return typeSig.DefinitionAssembly?.IsCorLib() != true;
            }

            if (typeDef.IsValueType)
            {
                return IsDuckCopyValueType(typeDef);
            }

            return false;
        }

        /// <summary>
        /// Determines whether duck copy value type.
        /// </summary>
        /// <param name="typeDef">The type def value.</param>
        /// <returns>true if the operation succeeds; otherwise, false.</returns>
        private static bool IsDuckCopyValueType(TypeDef typeDef)
        {
            foreach (var customAttribute in typeDef.CustomAttributes)
            {
                if (string.Equals(customAttribute.TypeFullName, DuckCopyAttributeTypeName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Resolves an inherited interface type for proxy traversal.
        /// </summary>
        /// <param name="ownerType">The type that owns the interface reference.</param>
        /// <param name="interfaceReference">The interface reference.</param>
        /// <returns>The resolved interface definition when available; otherwise, null.</returns>
        private static TypeDef? ResolveInterfaceTypeDefForTraversal(TypeDef ownerType, ITypeDefOrRef interfaceReference)
        {
            var resolvedInterface = interfaceReference.ResolveTypeDef();
            if (resolvedInterface is not null)
            {
                return resolvedInterface;
            }

            var interfaceTypeName = interfaceReference.ReflectionFullName?.Replace('/', '+')
                                 ?? interfaceReference.FullName.Replace('/', '+');
            var interfaceAssemblyName = interfaceReference.DefinitionAssembly?.Name?.String ?? string.Empty;
            if (!TryResolveRuntimeType(interfaceAssemblyName, assemblyPath: string.Empty, interfaceTypeName, out var runtimeInterfaceType) ||
                runtimeInterfaceType is null)
            {
                return null;
            }

            var runtimeInterfaceDefinition = GetRuntimeTypeDefinition(runtimeInterfaceType);
            var runtimeAssemblyPath = runtimeInterfaceDefinition.Assembly.Location;
            if (StringUtil.IsNullOrWhiteSpace(runtimeAssemblyPath) ||
                !File.Exists(runtimeAssemblyPath))
            {
                return null;
            }

            var moduleContext = ownerType.Module?.Context ?? ModuleDef.CreateModuleContext();
            if (moduleContext.AssemblyResolver is AssemblyResolver assemblyResolver)
            {
                AddAssemblyResolverSearchPath(assemblyResolver, Path.GetDirectoryName(runtimeAssemblyPath));
            }

            var runtimeModule = ModuleDefMD.Load(runtimeAssemblyPath, moduleContext);
            runtimeModule.EnableTypeDefFindCache = true;
            if (moduleContext.AssemblyResolver is AssemblyResolver resolver)
            {
                resolver.AddToCache(runtimeModule);
            }

            var runtimeMetadataName = runtimeInterfaceDefinition.FullName?.Replace('+', '/') ?? interfaceTypeName.Replace('+', '/');
            return TryResolveType(runtimeModule, runtimeMetadataName, out var runtimeInterfaceDefinitionType)
                       ? runtimeInterfaceDefinitionType
                       : null;
        }

        /// <summary>
        /// Represents forward method binding info.
        /// </summary>
        private readonly struct ForwardMethodBindingInfo
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ForwardMethodBindingInfo"/> struct.
            /// </summary>
            /// <param name="parameterBindings">The parameter bindings value.</param>
            /// <param name="returnConversion">The return conversion value.</param>
            /// <param name="closedGenericMethodArguments">The closed generic method arguments value.</param>
            internal ForwardMethodBindingInfo(
                IReadOnlyList<MethodParameterBinding> parameterBindings,
                MethodReturnConversion returnConversion,
                IReadOnlyList<TypeSig>? closedGenericMethodArguments,
                int trailingOptionalTargetParameterCount)
            {
                ParameterBindings = parameterBindings;
                ReturnConversion = returnConversion;
                ClosedGenericMethodArguments = closedGenericMethodArguments;
                TrailingOptionalTargetParameterCount = trailingOptionalTargetParameterCount;
            }

            /// <summary>
            /// Gets parameter bindings.
            /// </summary>
            /// <value>The parameter bindings value.</value>
            internal IReadOnlyList<MethodParameterBinding> ParameterBindings { get; }

            /// <summary>
            /// Gets return conversion.
            /// </summary>
            /// <value>The return conversion value.</value>
            internal MethodReturnConversion ReturnConversion { get; }

            /// <summary>
            /// Gets closed generic method arguments.
            /// </summary>
            /// <value>The closed generic method arguments value.</value>
            internal IReadOnlyList<TypeSig>? ClosedGenericMethodArguments { get; }

            /// <summary>
            /// Gets the number of trailing target parameters that should be emitted as optional defaults.
            /// </summary>
            /// <value>The trailing optional target parameter count value.</value>
            internal int TrailingOptionalTargetParameterCount { get; }
        }

        /// <summary>
        /// Represents method parameter binding.
        /// </summary>
        private readonly struct MethodParameterBinding
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="MethodParameterBinding"/> struct.
            /// </summary>
            /// <param name="isByRef">The is by ref value.</param>
            /// <param name="useLocalForByRef">The use local for by ref value.</param>
            /// <param name="isOut">The is out value.</param>
            /// <param name="proxyTypeSig">The proxy type sig value.</param>
            /// <param name="targetTypeSig">The target type sig value.</param>
            /// <param name="proxyByRefElementTypeSig">The proxy by ref element type sig value.</param>
            /// <param name="targetByRefElementTypeSig">The target by ref element type sig value.</param>
            /// <param name="preCallConversion">The pre call conversion value.</param>
            /// <param name="postCallConversion">The post call conversion value.</param>
            private MethodParameterBinding(
                bool isByRef,
                bool useLocalForByRef,
                bool isOut,
                TypeSig proxyTypeSig,
                TypeSig targetTypeSig,
                TypeSig? proxyByRefElementTypeSig,
                TypeSig? targetByRefElementTypeSig,
                MethodArgumentConversion preCallConversion,
                MethodReturnConversion postCallConversion)
            {
                IsByRef = isByRef;
                UseLocalForByRef = useLocalForByRef;
                IsOut = isOut;
                ProxyTypeSig = proxyTypeSig;
                TargetTypeSig = targetTypeSig;
                ProxyByRefElementTypeSig = proxyByRefElementTypeSig;
                TargetByRefElementTypeSig = targetByRefElementTypeSig;
                PreCallConversion = preCallConversion;
                PostCallConversion = postCallConversion;
            }

            /// <summary>
            /// Gets a value indicating whether is by ref.
            /// </summary>
            /// <value>The is by ref value.</value>
            internal bool IsByRef { get; }

            /// <summary>
            /// Gets a value indicating whether use local for by ref.
            /// </summary>
            /// <value>The use local for by ref value.</value>
            internal bool UseLocalForByRef { get; }

            /// <summary>
            /// Gets a value indicating whether is out.
            /// </summary>
            /// <value>The is out value.</value>
            internal bool IsOut { get; }

            /// <summary>
            /// Gets proxy type sig.
            /// </summary>
            /// <value>The proxy type sig value.</value>
            internal TypeSig ProxyTypeSig { get; }

            /// <summary>
            /// Gets target type sig.
            /// </summary>
            /// <value>The target type sig value.</value>
            internal TypeSig TargetTypeSig { get; }

            /// <summary>
            /// Gets proxy by ref element type sig.
            /// </summary>
            /// <value>The proxy by ref element type sig value.</value>
            internal TypeSig? ProxyByRefElementTypeSig { get; }

            /// <summary>
            /// Gets target by ref element type sig.
            /// </summary>
            /// <value>The target by ref element type sig value.</value>
            internal TypeSig? TargetByRefElementTypeSig { get; }

            /// <summary>
            /// Gets pre call conversion.
            /// </summary>
            /// <value>The pre call conversion value.</value>
            internal MethodArgumentConversion PreCallConversion { get; }

            /// <summary>
            /// Gets post call conversion.
            /// </summary>
            /// <value>The post call conversion value.</value>
            internal MethodReturnConversion PostCallConversion { get; }

            /// <summary>
            /// Creates a parameter binding for standard (non-byref) arguments.
            /// </summary>
            /// <param name="proxyTypeSig">The proxy type sig value.</param>
            /// <param name="targetTypeSig">The target type sig value.</param>
            /// <param name="preCallConversion">The pre call conversion value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static MethodParameterBinding ForStandard(TypeSig proxyTypeSig, TypeSig targetTypeSig, MethodArgumentConversion preCallConversion)
            {
                return new MethodParameterBinding(
                    isByRef: false,
                    useLocalForByRef: false,
                    isOut: false,
                    proxyTypeSig,
                    targetTypeSig,
                    proxyByRefElementTypeSig: null,
                    targetByRefElementTypeSig: null,
                    preCallConversion,
                    MethodReturnConversion.None());
            }

            /// <summary>
            /// Creates a by-ref parameter binding that passes through directly.
            /// </summary>
            /// <param name="proxyTypeSig">The proxy type sig value.</param>
            /// <param name="targetTypeSig">The target type sig value.</param>
            /// <param name="proxyByRefElementTypeSig">The proxy by ref element type sig value.</param>
            /// <param name="targetByRefElementTypeSig">The target by ref element type sig value.</param>
            /// <param name="isOut">The is out value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static MethodParameterBinding ForByRefDirect(
                TypeSig proxyTypeSig,
                TypeSig targetTypeSig,
                TypeSig proxyByRefElementTypeSig,
                TypeSig targetByRefElementTypeSig,
                bool isOut)
            {
                return new MethodParameterBinding(
                    isByRef: true,
                    useLocalForByRef: false,
                    isOut,
                    proxyTypeSig,
                    targetTypeSig,
                    proxyByRefElementTypeSig,
                    targetByRefElementTypeSig,
                    MethodArgumentConversion.None(),
                    MethodReturnConversion.None());
            }

            /// <summary>
            /// Creates a by-ref parameter binding that stages through a local temporary.
            /// </summary>
            /// <param name="proxyTypeSig">The proxy type sig value.</param>
            /// <param name="targetTypeSig">The target type sig value.</param>
            /// <param name="proxyByRefElementTypeSig">The proxy by ref element type sig value.</param>
            /// <param name="targetByRefElementTypeSig">The target by ref element type sig value.</param>
            /// <param name="isOut">The is out value.</param>
            /// <param name="preCallConversion">The pre call conversion value.</param>
            /// <param name="postCallConversion">The post call conversion value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static MethodParameterBinding ForByRefWithLocal(
                TypeSig proxyTypeSig,
                TypeSig targetTypeSig,
                TypeSig proxyByRefElementTypeSig,
                TypeSig targetByRefElementTypeSig,
                bool isOut,
                MethodArgumentConversion preCallConversion,
                MethodReturnConversion postCallConversion)
            {
                return new MethodParameterBinding(
                    isByRef: true,
                    useLocalForByRef: true,
                    isOut,
                    proxyTypeSig,
                    targetTypeSig,
                    proxyByRefElementTypeSig,
                    targetByRefElementTypeSig,
                    preCallConversion,
                    postCallConversion);
            }
        }

        /// <summary>
        /// Represents method argument conversion.
        /// </summary>
        private readonly struct MethodArgumentConversion
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="MethodArgumentConversion"/> struct.
            /// </summary>
            /// <param name="kind">The kind value.</param>
            /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
            /// <param name="innerTypeSig">The inner type sig value.</param>
            /// <param name="unwrapWrapperTypeSig">The unwrap wrapper type sig value.</param>
            /// <param name="unwrapInnerTypeSig">The unwrap inner type sig value.</param>
            private MethodArgumentConversion(
                MethodArgumentConversionKind kind,
                TypeSig? wrapperTypeSig,
                TypeSig? innerTypeSig,
                TypeSig? unwrapWrapperTypeSig,
                TypeSig? unwrapInnerTypeSig)
            {
                Kind = kind;
                WrapperTypeSig = wrapperTypeSig;
                InnerTypeSig = innerTypeSig;
                UnwrapWrapperTypeSig = unwrapWrapperTypeSig;
                UnwrapInnerTypeSig = unwrapInnerTypeSig;
            }

            /// <summary>
            /// Gets kind.
            /// </summary>
            /// <value>The kind value.</value>
            internal MethodArgumentConversionKind Kind { get; }

            /// <summary>
            /// Gets wrapper type sig.
            /// </summary>
            /// <value>The wrapper type sig value.</value>
            internal TypeSig? WrapperTypeSig { get; }

            /// <summary>
            /// Gets inner type sig.
            /// </summary>
            /// <value>The inner type sig value.</value>
            internal TypeSig? InnerTypeSig { get; }

            /// <summary>
            /// Gets unwrap wrapper type sig.
            /// </summary>
            /// <value>The unwrap wrapper type sig value.</value>
            internal TypeSig? UnwrapWrapperTypeSig { get; }

            /// <summary>
            /// Gets unwrap inner type sig.
            /// </summary>
            /// <value>The unwrap inner type sig value.</value>
            internal TypeSig? UnwrapInnerTypeSig { get; }

            /// <summary>
            /// Gets a value indicating whether ValueWithType unwrapping is required.
            /// </summary>
            /// <value>The requires value with type unwrap value.</value>
            internal bool RequiresValueWithTypeUnwrap => UnwrapWrapperTypeSig is not null;

            /// <summary>
            /// Creates a no-op conversion descriptor.
            /// </summary>
            /// <returns>The result produced by this operation.</returns>
            internal static MethodArgumentConversion None()
            {
                return new MethodArgumentConversion(
                    MethodArgumentConversionKind.None,
                    wrapperTypeSig: null,
                    innerTypeSig: null,
                    unwrapWrapperTypeSig: null,
                    unwrapInnerTypeSig: null);
            }

            /// <summary>
            /// Creates a conversion that unwraps ValueWithType<T>.
            /// </summary>
            /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
            /// <param name="innerTypeSig">The inner type sig value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static MethodArgumentConversion UnwrapValueWithType(TypeSig wrapperTypeSig, TypeSig innerTypeSig)
            {
                return new MethodArgumentConversion(
                    MethodArgumentConversionKind.UnwrapValueWithType,
                    wrapperTypeSig,
                    innerTypeSig,
                    unwrapWrapperTypeSig: null,
                    unwrapInnerTypeSig: null);
            }

            /// <summary>
            /// Creates a conversion that extracts IDuckType.Instance.
            /// </summary>
            /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
            /// <param name="innerTypeSig">The inner type sig value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static MethodArgumentConversion ExtractDuckTypeInstance(TypeSig wrapperTypeSig, TypeSig innerTypeSig)
            {
                return new MethodArgumentConversion(
                    MethodArgumentConversionKind.ExtractDuckTypeInstance,
                    wrapperTypeSig,
                    innerTypeSig,
                    unwrapWrapperTypeSig: null,
                    unwrapInnerTypeSig: null);
            }

            /// <summary>
            /// Creates a conversion that chains through DuckType.CreateCache&lt;T&gt;.
            /// </summary>
            /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
            /// <param name="innerTypeSig">The inner type sig value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static MethodArgumentConversion DuckChainToProxy(TypeSig wrapperTypeSig, TypeSig innerTypeSig)
            {
                return new MethodArgumentConversion(
                    MethodArgumentConversionKind.DuckChainToProxy,
                    wrapperTypeSig,
                    innerTypeSig,
                    unwrapWrapperTypeSig: null,
                    unwrapInnerTypeSig: null);
            }

            /// <summary>
            /// Creates a conversion that applies runtime type adaptation.
            /// </summary>
            /// <param name="actualTypeSig">The actual type sig value.</param>
            /// <param name="expectedTypeSig">The expected type sig value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static MethodArgumentConversion TypeConversion(TypeSig actualTypeSig, TypeSig expectedTypeSig)
            {
                return new MethodArgumentConversion(
                    MethodArgumentConversionKind.TypeConversion,
                    actualTypeSig,
                    expectedTypeSig,
                    unwrapWrapperTypeSig: null,
                    unwrapInnerTypeSig: null);
            }

            /// <summary>
            /// Creates a conversion that first unwraps ValueWithType&lt;T&gt; before applying this conversion.
            /// </summary>
            /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
            /// <param name="innerTypeSig">The inner type sig value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal MethodArgumentConversion WithValueWithTypeUnwrap(TypeSig wrapperTypeSig, TypeSig innerTypeSig)
            {
                return new MethodArgumentConversion(
                    Kind,
                    WrapperTypeSig,
                    InnerTypeSig,
                    wrapperTypeSig,
                    innerTypeSig);
            }
        }

        /// <summary>
        /// Represents method return conversion.
        /// </summary>
        private readonly struct MethodReturnConversion
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="MethodReturnConversion"/> struct.
            /// </summary>
            /// <param name="kind">The kind value.</param>
            /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
            /// <param name="innerTypeSig">The inner type sig value.</param>
            /// <param name="isReverseDuckChaining">Whether chaining calls the reverse factory.</param>
            private MethodReturnConversion(MethodReturnConversionKind kind, TypeSig? wrapperTypeSig, TypeSig? innerTypeSig, bool isReverseDuckChaining = false, bool keepsNull = true)
            {
                Kind = kind;
                WrapperTypeSig = wrapperTypeSig;
                InnerTypeSig = innerTypeSig;
                IsReverseDuckChaining = isReverseDuckChaining;
                KeepsNull = keepsNull;
            }

            /// <summary>
            /// Gets kind.
            /// </summary>
            /// <value>The kind value.</value>
            internal MethodReturnConversionKind Kind { get; }

            /// <summary>
            /// Gets wrapper type sig.
            /// </summary>
            /// <value>The wrapper type sig value.</value>
            internal TypeSig? WrapperTypeSig { get; }

            /// <summary>
            /// Gets inner type sig.
            /// </summary>
            /// <value>The inner type sig value.</value>
            internal TypeSig? InnerTypeSig { get; }

            /// <summary>
            /// Gets whether chaining calls the reverse factory.
            /// </summary>
            internal bool IsReverseDuckChaining { get; }

            /// <summary>
            /// Gets whether extracting the instance of a null duck type gives null (otherwise, like dynamic duck typing's reverse
            /// proxies, it throws a NullReferenceException).
            /// </summary>
            internal bool KeepsNull { get; }

            /// <summary>
            /// Creates a no-op conversion descriptor.
            /// </summary>
            /// <returns>The result produced by this operation.</returns>
            internal static MethodReturnConversion None()
            {
                return new MethodReturnConversion(MethodReturnConversionKind.None, wrapperTypeSig: null, innerTypeSig: null);
            }

            /// <summary>
            /// Creates a conversion that wraps a value into ValueWithType<T>.
            /// </summary>
            /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
            /// <param name="sourceTypeSig">The source type sig value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static MethodReturnConversion WrapValueWithType(TypeSig wrapperTypeSig, TypeSig sourceTypeSig)
            {
                return new MethodReturnConversion(MethodReturnConversionKind.WrapValueWithType, wrapperTypeSig, sourceTypeSig);
            }

            /// <summary>
            /// Creates a conversion that duck-chains then wraps into ValueWithType<T>.
            /// </summary>
            /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
            /// <param name="sourceTypeSig">The source type sig value.</param>
            /// <param name="reverse">Whether chaining calls the reverse factory.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static MethodReturnConversion WrapValueWithTypeAfterDuckChainToProxy(TypeSig wrapperTypeSig, TypeSig sourceTypeSig, bool reverse = false)
            {
                return new MethodReturnConversion(MethodReturnConversionKind.WrapValueWithTypeAfterDuckChainToProxy, wrapperTypeSig, sourceTypeSig, reverse);
            }

            /// <summary>
            /// Creates a conversion that applies type conversion then wraps into ValueWithType<T>.
            /// </summary>
            /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
            /// <param name="sourceTypeSig">The source type sig value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static MethodReturnConversion WrapValueWithTypeAfterTypeConversion(TypeSig wrapperTypeSig, TypeSig sourceTypeSig)
            {
                return new MethodReturnConversion(MethodReturnConversionKind.WrapValueWithTypeAfterTypeConversion, wrapperTypeSig, sourceTypeSig);
            }

            /// <summary>
            /// Creates a conversion that chains through DuckType.CreateCache<T>.
            /// </summary>
            /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
            /// <param name="innerTypeSig">The inner type sig value.</param>
            /// <param name="reverse">Whether chaining calls the reverse factory.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static MethodReturnConversion DuckChainToProxy(TypeSig wrapperTypeSig, TypeSig innerTypeSig, bool reverse = false)
            {
                return new MethodReturnConversion(MethodReturnConversionKind.DuckChainToProxy, wrapperTypeSig, innerTypeSig, reverse);
            }

            /// <summary>
            /// Creates a conversion that extracts IDuckType.Instance.
            /// </summary>
            /// <param name="wrapperTypeSig">The wrapper type sig value.</param>
            /// <param name="innerTypeSig">The inner type sig value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static MethodReturnConversion ExtractDuckTypeInstance(TypeSig wrapperTypeSig, TypeSig innerTypeSig, bool keepsNull = true)
            {
                return new MethodReturnConversion(MethodReturnConversionKind.ExtractDuckTypeInstance, wrapperTypeSig, innerTypeSig, keepsNull: keepsNull);
            }

            /// <summary>
            /// Creates a conversion that applies runtime type adaptation.
            /// </summary>
            /// <param name="actualTypeSig">The actual type sig value.</param>
            /// <param name="expectedTypeSig">The expected type sig value.</param>
            /// <returns>The result produced by this operation.</returns>
            internal static MethodReturnConversion TypeConversion(TypeSig actualTypeSig, TypeSig expectedTypeSig)
            {
                return new MethodReturnConversion(MethodReturnConversionKind.TypeConversion, actualTypeSig, expectedTypeSig);
            }
        }

        /// <summary>
        /// Represents a forward target method candidate and the configured proxy-name alternative that found it.
        /// </summary>
        private readonly struct ForwardMethodCandidate
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ForwardMethodCandidate"/> struct.
            /// </summary>
            /// <param name="method">The method value.</param>
            /// <param name="nameOrdinal">The configured name ordinal value.</param>
            internal ForwardMethodCandidate(MethodDef method, int nameOrdinal)
            {
                Method = method;
                NameOrdinal = nameOrdinal;
            }

            /// <summary>
            /// Gets method.
            /// </summary>
            /// <value>The method value.</value>
            internal MethodDef Method { get; }

            /// <summary>
            /// Gets name ordinal.
            /// </summary>
            /// <value>The name ordinal value.</value>
            internal int NameOrdinal { get; }
        }

        private sealed class ForwardMethodBindingPlanCacheEntry
        {
            internal ForwardMethodBindingPlanCacheEntry(ForwardMethodBindingInfo binding)
            {
                Succeeded = true;
                Binding = binding;
            }

            internal ForwardMethodBindingPlanCacheEntry(string detail)
            {
                Succeeded = false;
                FailureDetail = detail;
            }

            internal bool Succeeded { get; }

            internal ForwardMethodBindingInfo Binding { get; }

            internal string? FailureDetail { get; }
        }

        private sealed class MethodReturnConversionCacheEntry
        {
            internal MethodReturnConversionCacheEntry(MethodReturnConversion conversion)
            {
                Succeeded = true;
                Conversion = conversion;
            }

            internal MethodReturnConversionCacheEntry()
            {
                Succeeded = false;
            }

            internal bool Succeeded { get; }

            internal MethodReturnConversion Conversion { get; }
        }

        private sealed class MethodArgumentConversionCacheEntry
        {
            internal MethodArgumentConversionCacheEntry(MethodArgumentConversion conversion)
            {
                Succeeded = true;
                Conversion = conversion;
            }

            internal MethodArgumentConversionCacheEntry()
            {
                Succeeded = false;
            }

            internal bool Succeeded { get; }

            internal MethodArgumentConversion Conversion { get; }
        }

        private sealed class TargetTypePlan
        {
            private readonly TypeDef _targetType;
            private readonly IReadOnlyDictionary<string, IReadOnlyList<TargetMethodCandidate>> _methodsByExactName;
            private readonly IReadOnlyDictionary<string, IReadOnlyList<TargetMethodCandidate>> _methodsBySimpleName;
            private readonly IReadOnlyDictionary<string, IReadOnlyList<TargetMethodCandidate>> _methodsByExactNameAndShape;
            private readonly IReadOnlyDictionary<string, IReadOnlyList<TargetMethodCandidate>> _methodsBySimpleNameAndShape;
            private readonly IReadOnlyDictionary<string, IReadOnlyList<TargetPropertyCandidate>> _propertiesByName;
            private readonly IReadOnlyDictionary<string, IReadOnlyList<TargetFieldCandidate>> _fieldsByName;
            private readonly Dictionary<string, IReadOnlyList<ForwardMethodCandidate>> _forwardMethodCandidatesByKey = new(StringComparer.Ordinal);
            private bool _reverseMetadataInitialized;
            private bool _hasReverseMethodAttributes;
            private IReadOnlyList<MethodDef>? _declaredReverseImplementationMethods;

            internal TargetTypePlan(TypeDef targetType)
            {
                _targetType = targetType;
                IdentityKey = BuildTypeIdentityKey(targetType);
                var hierarchy = new List<TypeDef>();
                var current = targetType;
                while (current is not null)
                {
                    hierarchy.Add(current);
                    current = current.BaseType?.ResolveTypeDef();
                }

                Hierarchy = hierarchy;

                var exactMethodIndex = new Dictionary<string, List<TargetMethodCandidate>>(StringComparer.Ordinal);
                var simpleMethodIndex = new Dictionary<string, List<TargetMethodCandidate>>(StringComparer.Ordinal);
                var exactMethodShapeIndex = new Dictionary<string, List<TargetMethodCandidate>>(StringComparer.Ordinal);
                var simpleMethodShapeIndex = new Dictionary<string, List<TargetMethodCandidate>>(StringComparer.Ordinal);
                var propertyIndex = new Dictionary<string, List<TargetPropertyCandidate>>(StringComparer.Ordinal);
                var fieldIndex = new Dictionary<string, List<TargetFieldCandidate>>(StringComparer.Ordinal);

                foreach (var hierarchyType in hierarchy)
                {
                    var isInherited = !ReferenceEquals(hierarchyType, targetType);
                    foreach (var method in hierarchyType.Methods)
                    {
                        var candidate = new TargetMethodCandidate(method, isInherited);
                        var methodName = method.Name.String ?? method.Name.ToString();
                        AddIndexEntry(exactMethodIndex, methodName, candidate);
                        AddIndexEntry(exactMethodShapeIndex, BuildMethodShapeIndexKey(methodName, method), candidate);

                        var simpleName = ResolveSimpleMethodName(methodName);
                        if (!StringUtil.IsNullOrWhiteSpace(simpleName))
                        {
                            AddIndexEntry(simpleMethodIndex, simpleName!, candidate);
                            AddIndexEntry(simpleMethodShapeIndex, BuildMethodShapeIndexKey(simpleName!, method), candidate);
                        }
                    }

                    foreach (var property in hierarchyType.Properties)
                    {
                        AddIndexEntry(propertyIndex, property.Name.String ?? property.Name.ToString(), new TargetPropertyCandidate(property, isInherited));
                    }

                    foreach (var field in hierarchyType.Fields)
                    {
                        AddIndexEntry(fieldIndex, field.Name.String ?? field.Name.ToString(), new TargetFieldCandidate(field, isInherited));
                    }
                }

                _methodsByExactName = exactMethodIndex.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyList<TargetMethodCandidate>)kvp.Value, StringComparer.Ordinal);
                _methodsBySimpleName = simpleMethodIndex.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyList<TargetMethodCandidate>)kvp.Value, StringComparer.Ordinal);
                _methodsByExactNameAndShape = exactMethodShapeIndex.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyList<TargetMethodCandidate>)kvp.Value, StringComparer.Ordinal);
                _methodsBySimpleNameAndShape = simpleMethodShapeIndex.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyList<TargetMethodCandidate>)kvp.Value, StringComparer.Ordinal);
                _propertiesByName = propertyIndex.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyList<TargetPropertyCandidate>)kvp.Value, StringComparer.Ordinal);
                _fieldsByName = fieldIndex.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyList<TargetFieldCandidate>)kvp.Value, StringComparer.Ordinal);
            }

            internal IReadOnlyList<TypeDef> Hierarchy { get; }

            internal string IdentityKey { get; }

            internal bool HasReverseMethodAttributes
            {
                get
                {
                    EnsureReverseMetadata();
                    return _hasReverseMethodAttributes;
                }
            }

            internal IReadOnlyList<MethodDef> DeclaredReverseImplementationMethods
            {
                get
                {
                    EnsureReverseMetadata();
                    return _declaredReverseImplementationMethods ?? Array.Empty<MethodDef>();
                }
            }

            internal IEnumerable<TargetMethodCandidate> GetMethodCandidatesByExactNameAndShape(string methodName, int genericArity, int parameterCount)
                => _methodsByExactNameAndShape.TryGetValue(BuildMethodShapeIndexKey(methodName, genericArity, parameterCount), out var candidates) ? candidates : Array.Empty<TargetMethodCandidate>();

            internal IEnumerable<TargetMethodCandidate> GetMethodCandidatesBySimpleNameAndShape(string methodName, int genericArity, int parameterCount)
                => _methodsBySimpleNameAndShape.TryGetValue(BuildMethodShapeIndexKey(methodName, genericArity, parameterCount), out var candidates) ? candidates : Array.Empty<TargetMethodCandidate>();

            internal IEnumerable<TargetPropertyCandidate> GetPropertyCandidates(string propertyName, bool useIgnoreCaseMemberMatching)
            {
                if (!useIgnoreCaseMemberMatching)
                {
                    return _propertiesByName.TryGetValue(propertyName, out var candidates) ? candidates : Array.Empty<TargetPropertyCandidate>();
                }

                return _propertiesByName
                      .Where(kvp => string.Equals(kvp.Key, propertyName, StringComparison.OrdinalIgnoreCase))
                      .SelectMany(kvp => kvp.Value);
            }

            internal IEnumerable<TargetFieldCandidate> GetFieldCandidates(string fieldName, bool useIgnoreCaseMemberMatching)
            {
                if (!useIgnoreCaseMemberMatching)
                {
                    return _fieldsByName.TryGetValue(fieldName, out var candidates) ? candidates : Array.Empty<TargetFieldCandidate>();
                }

                return _fieldsByName
                      .Where(kvp => string.Equals(kvp.Key, fieldName, StringComparison.OrdinalIgnoreCase))
                      .SelectMany(kvp => kvp.Value);
            }

            internal IReadOnlyList<ForwardMethodCandidate> GetForwardMethodCandidates(
                ProxyMethodPlan proxyMethodPlan,
                IReadOnlyList<string> explicitInterfaceTypeNames,
                bool useRelaxedNameComparison,
                int expectedGenericArity,
                IReadOnlyList<string> configuredParameterTypeNames,
                bool allowPrivateBaseMembers,
                bool allowTrailingOptionalTargetParameters)
            {
                var phaseStopwatch = StartProfilePhase();
                var profile = _currentProfile;
                try
                {
                    var cacheKey = string.Concat(
                        proxyMethodPlan.ForwardTargetMethodNamesCacheKey,
                        "::",
                        proxyMethodPlan.ExplicitInterfaceTypeNamesCacheKey,
                        "::",
                        useRelaxedNameComparison ? "relaxed" : "strict",
                        "::",
                        expectedGenericArity.ToString(CultureInfo.InvariantCulture),
                        "::",
                        proxyMethodPlan.ParameterCountCacheKey,
                        "::",
                        allowPrivateBaseMembers ? "base" : "declared",
                        "::",
                        proxyMethodPlan.DuckBindingFlagsCacheKey,
                        "::",
                        allowTrailingOptionalTargetParameters ? "optional" : "exact",
                        "::",
                        proxyMethodPlan.ConfiguredParameterTypeNamesCacheKey);
                    if (_forwardMethodCandidatesByKey.TryGetValue(cacheKey, out var cachedCandidates))
                    {
                        if (profile is not null)
                        {
                            profile.ForwardCandidateListCacheHits++;
                        }

                        return cachedCandidates;
                    }

                    if (profile is not null)
                    {
                        profile.ForwardCandidateListCacheMisses++;
                    }

                    var proxyParameterCount = proxyMethodPlan.Method.MethodSig.Params.Count;
                    var emittedCandidates = new HashSet<string>(StringComparer.Ordinal);
                    var candidates = new List<ForwardMethodCandidate>();
                    for (var candidateMethodNameIndex = 0; candidateMethodNameIndex < proxyMethodPlan.ForwardTargetMethodNames.Count; candidateMethodNameIndex++)
                    {
                        var candidateMethodName = proxyMethodPlan.ForwardTargetMethodNames[candidateMethodNameIndex];
                        foreach (var candidateEntry in GetForwardMethodCandidateEntries(candidateMethodName, expectedGenericArity, proxyParameterCount, proxyMethodPlan.UseIgnoreCaseMemberMatching, allowTrailingOptionalTargetParameters))
                        {
                            if (profile is not null)
                            {
                                profile.ForwardCandidateEnumeratedCount++;
                            }

                            var candidate = candidateEntry.Method;
                            if (!emittedCandidates.Add(GetMethodCandidateKey(candidate)))
                            {
                                if (profile is not null)
                                {
                                    profile.ForwardCandidateDedupRejectCount++;
                                }

                                continue;
                            }

                            var candidateMethodActualName = candidateEntry.MethodName;
                            if (!IsForwardTargetMethodNameMatch(
                                    candidateMethodActualName,
                                    candidateMethodName,
                                    explicitInterfaceTypeNames,
                                    useRelaxedNameComparison,
                                    proxyMethodPlan.UseIgnoreCaseMemberMatching))
                            {
                                if (profile is not null)
                                {
                                    profile.ForwardCandidateNameRejectCount++;
                                }

                                continue;
                            }

                            if (!allowPrivateBaseMembers &&
                                candidateEntry.IsInherited &&
                                candidate.IsPrivate)
                            {
                                if (profile is not null)
                                {
                                    profile.ForwardCandidatePrivateRejectCount++;
                                }

                                continue;
                            }

                            if (!IsMethodCandidateAllowedByBindingFlags(candidateEntry, proxyMethodPlan.DuckBindingFlags, allowPrivateBaseMembers))
                            {
                                if (profile is not null)
                                {
                                    profile.ForwardCandidatePrivateRejectCount++;
                                }

                                continue;
                            }

                            if (profile is not null)
                            {
                                profile.ForwardCandidateAcceptedCount++;
                            }

                            candidates.Add(new ForwardMethodCandidate(candidate, candidateMethodNameIndex));
                        }
                    }

                    _forwardMethodCandidatesByKey[cacheKey] = candidates;
                    return candidates;
                }
                finally
                {
                    StopProfilePhase(phaseStopwatch, seconds => _currentProfile!.ForwardCandidateListBuildSeconds += seconds);
                }
            }

            private static bool IsSimpleMethodNameMatch(string methodName, string requestedMethodName, StringComparison comparison)
            {
                var simpleName = ResolveSimpleMethodName(methodName);
                return simpleName is not null && string.Equals(simpleName, requestedMethodName, comparison);
            }

            private static void AddIndexEntry<TCandidate>(IDictionary<string, List<TCandidate>> index, string key, TCandidate candidate)
            {
                if (!index.TryGetValue(key, out var candidates))
                {
                    candidates = [];
                    index[key] = candidates;
                }

                candidates.Add(candidate);
            }

            private static string? ResolveSimpleMethodName(string methodName)
            {
                var separatorIndex = methodName.LastIndexOf('.');
                if (separatorIndex < 0 || separatorIndex == methodName.Length - 1)
                {
                    return null;
                }

                return methodName.Substring(separatorIndex + 1);
            }

            private static string BuildMethodShapeIndexKey(string methodName, MethodDef method)
            {
                return BuildMethodShapeIndexKey(methodName, (int)method.MethodSig.GenParamCount, method.MethodSig.Params.Count);
            }

            private static string BuildMethodShapeIndexKey(string methodName, int genericArity, int parameterCount)
            {
                return string.Concat(methodName, "::", genericArity.ToString(CultureInfo.InvariantCulture), "::", parameterCount.ToString(CultureInfo.InvariantCulture));
            }

            private IEnumerable<TargetMethodCandidate> GetForwardMethodCandidateEntries(
                string candidateMethodName,
                int expectedGenericArity,
                int proxyParameterCount,
                bool useIgnoreCaseMemberMatching,
                bool allowTrailingOptionalTargetParameters)
            {
                if (!useIgnoreCaseMemberMatching && !allowTrailingOptionalTargetParameters)
                {
                    return GetMethodCandidatesByExactNameAndShape(candidateMethodName, expectedGenericArity, proxyParameterCount)
                          .Concat(GetMethodCandidatesBySimpleNameAndShape(candidateMethodName, expectedGenericArity, proxyParameterCount));
                }

                var comparison = useIgnoreCaseMemberMatching ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                return _methodsByExactName
                      .Where(kvp => string.Equals(kvp.Key, candidateMethodName, comparison) || IsSimpleMethodNameMatch(kvp.Key, candidateMethodName, comparison))
                      .SelectMany(kvp => kvp.Value)
                      .Where(candidate => candidate.Method.MethodSig.GenParamCount == expectedGenericArity &&
                                          IsParameterCountCompatibleWithProxy(candidate.Method, proxyParameterCount, allowTrailingOptionalTargetParameters));
            }

            private void EnsureReverseMetadata()
            {
                if (_reverseMetadataInitialized)
                {
                    return;
                }

                var declaredReverseImplementationMethods = new List<MethodDef>();
                var emittedReverseImplementationKeys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var hierarchyType in Hierarchy)
                {
                    var isDeclaredOnTargetType = ReferenceEquals(hierarchyType, _targetType);
                    foreach (var method in hierarchyType.Methods)
                    {
                        if (!_hasReverseMethodAttributes &&
                            method.CustomAttributes.Any(IsReverseMethodAttribute))
                        {
                            _hasReverseMethodAttributes = true;
                        }

                        if (!isDeclaredOnTargetType ||
                            method.IsConstructor ||
                            method.IsStatic ||
                            !GetReverseMethodAttributes(method).Any())
                        {
                            continue;
                        }

                        var methodKey = GetMethodCandidateKey(method);
                        if (emittedReverseImplementationKeys.Add(methodKey))
                        {
                            declaredReverseImplementationMethods.Add(method);
                        }
                    }

                    foreach (var property in hierarchyType.Properties)
                    {
                        if (!_hasReverseMethodAttributes &&
                            property.CustomAttributes.Any(IsReverseMethodAttribute))
                        {
                            _hasReverseMethodAttributes = true;
                        }

                        if (!IsReverseImplementationPropertyVisibleToDynamic(property) ||
                            !property.CustomAttributes.Any(IsReverseMethodAttribute))
                        {
                            continue;
                        }

                        if (property.GetMethod is not null)
                        {
                            var getterKey = GetMethodCandidateKey(property.GetMethod);
                            if (emittedReverseImplementationKeys.Add(getterKey))
                            {
                                declaredReverseImplementationMethods.Add(property.GetMethod);
                            }
                        }

                        if (property.SetMethod is not null)
                        {
                            var setterKey = GetMethodCandidateKey(property.SetMethod);
                            if (emittedReverseImplementationKeys.Add(setterKey))
                            {
                                declaredReverseImplementationMethods.Add(property.SetMethod);
                            }
                        }
                    }
                }

                _declaredReverseImplementationMethods = declaredReverseImplementationMethods;
                _reverseMetadataInitialized = true;
            }
        }

        private sealed class TargetMethodCandidate
        {
            internal TargetMethodCandidate(MethodDef method, bool isInherited)
            {
                Method = method;
                MethodName = method.Name.String ?? method.Name.ToString();
                IsInherited = isInherited;
            }

            internal MethodDef Method { get; }

            internal string MethodName { get; }

            internal bool IsInherited { get; }
        }

        private sealed class TargetPropertyCandidate
        {
            internal TargetPropertyCandidate(PropertyDef property, bool isInherited)
            {
                Property = property;
                IsInherited = isInherited;
                IsEffectivelyPrivate = IsPropertyPrivate(property);
            }

            internal PropertyDef Property { get; }

            internal bool IsInherited { get; }

            internal bool IsEffectivelyPrivate { get; }
        }

        private sealed class TargetFieldCandidate
        {
            internal TargetFieldCandidate(FieldDef field, bool isInherited)
            {
                Field = field;
                IsInherited = isInherited;
            }

            internal FieldDef Field { get; }

            internal bool IsInherited { get; }
        }

        internal sealed class InterfaceTraversalEntry
        {
            internal InterfaceTraversalEntry(TypeDef interfaceType, ITypeDefOrRef interfaceReference, TypeSig interfaceTypeSig, bool isProxyType, IReadOnlyList<TypeSig>? genericArguments)
            {
                InterfaceType = interfaceType;
                InterfaceReference = interfaceReference;
                InterfaceTypeSig = interfaceTypeSig;
                IsProxyType = isProxyType;
                GenericArguments = genericArguments;
            }

            internal TypeDef InterfaceType { get; }

            internal ITypeDefOrRef InterfaceReference { get; }

            internal TypeSig InterfaceTypeSig { get; }

            internal bool IsProxyType { get; }

            internal IReadOnlyList<TypeSig>? GenericArguments { get; }
        }

        private sealed class InterfaceMethodCollection
        {
            internal InterfaceMethodCollection(IReadOnlyList<MethodDef> methods, IReadOnlyDictionary<MethodDef, InterfaceMethodContract> contractsByMethod)
            {
                Methods = methods;
                ContractsByMethod = contractsByMethod;
            }

            internal IReadOnlyList<MethodDef> Methods { get; }

            internal IReadOnlyDictionary<MethodDef, InterfaceMethodContract> ContractsByMethod { get; }
        }

        private sealed class ProxyTypePlan
        {
            private readonly Dictionary<MethodDef, ProxyMethodPlan> _methodPlans = new(ReferenceIdentityComparer<MethodDef>.Instance);
            private readonly IReadOnlyDictionary<MethodDef, InterfaceMethodContract> _interfaceMethodContractsByMethod;
            private readonly IReadOnlyDictionary<MethodDef, PropertyDef> _propertiesByAccessorMethod;

            internal ProxyTypePlan(TypeDef proxyType)
            {
                var implementedAccessors = BuildImplementedAccessors(proxyType);
                ImplementedAccessors = implementedAccessors;
                var interfaceMethods = BuildInterfaceMethods(proxyType, includeDuckIgnored: false, implementedAccessors);
                var reverseInterfaceMethods = BuildInterfaceMethods(proxyType, includeDuckIgnored: true, implementedAccessors: null);
                InterfaceMethods = interfaceMethods.Methods;
                ReverseInterfaceMethods = reverseInterfaceMethods.Methods;
                var interfaceMethodContracts = new Dictionary<MethodDef, InterfaceMethodContract>(ReferenceIdentityComparer<MethodDef>.Instance);
                foreach (var contract in reverseInterfaceMethods.ContractsByMethod.Concat(interfaceMethods.ContractsByMethod))
                {
                    interfaceMethodContracts[contract.Key] = contract.Value;
                }

                _interfaceMethodContractsByMethod = interfaceMethodContracts;
                ClassMethods = BuildClassMethods(proxyType, includeDuckIgnored: false, implementedAccessors);
                ReverseClassMethods = BuildClassMethods(proxyType, includeDuckIgnored: true, implementedAccessors: null);
                SupportedBaseConstructor = BuildSupportedBaseConstructor(proxyType);
                _propertiesByAccessorMethod = BuildAccessorPropertyMap(proxyType);
            }

            internal IReadOnlyList<MethodDef> InterfaceMethods { get; }

            internal IReadOnlyList<MethodDef> ReverseInterfaceMethods { get; }

            /// <summary>
            /// Gets the property accessors a forward proxy implements (see BuildImplementedAccessors).
            /// </summary>
            internal IReadOnlyCollection<MethodDef> ImplementedAccessors { get; }

            internal IReadOnlyList<MethodDef> ClassMethods { get; }

            internal IReadOnlyList<MethodDef> ReverseClassMethods { get; }

            internal MethodDef? SupportedBaseConstructor { get; }

            internal bool TryGetPropertyFromAccessor(MethodDef accessorMethod, out PropertyDef property)
                => _propertiesByAccessorMethod.TryGetValue(accessorMethod, out property!);

            internal InterfaceMethodContract? GetInterfaceMethodContract(MethodDef method)
                => _interfaceMethodContractsByMethod.TryGetValue(method, out var contract) ? contract : null;

            internal ProxyMethodPlan GetOrCreateMethodPlan(MethodDef proxyMethod)
            {
                if (_methodPlans.TryGetValue(proxyMethod, out var cachedPlan))
                {
                    return cachedPlan;
                }

                var plan = new ProxyMethodPlan(proxyMethod);
                _methodPlans[proxyMethod] = plan;
                return plan;
            }

            private static InterfaceMethodCollection BuildInterfaceMethods(TypeDef interfaceType, bool includeDuckIgnored, HashSet<MethodDef>? implementedAccessors)
            {
                var results = new List<MethodDef>();
                var contractsByMethod = new Dictionary<MethodDef, InterfaceMethodContract>(ReferenceIdentityComparer<MethodDef>.Instance);
                var visitedMethods = new HashSet<string>(StringComparer.Ordinal);

                // A property of a generic interface implemented twice (IGen<int>, IGen<string>) is selected once, by name, for the
                // first instance (see BuildImplementedAccessors): its other accessors have no implementation, like in dynamic duck
                // typing's proxy type.
                var addedImplementedAccessors = new HashSet<MethodDef>(ReferenceIdentityComparer<MethodDef>.Instance);

                // The proxy interface, then its interfaces like Type.GetInterfaces() lists them: of two methods with the same
                // signature, dynamic duck typing implements the first one.
                var interfaces = new[] { new InterfaceTraversalEntry(interfaceType, interfaceType, interfaceType.ToTypeSig(), isProxyType: true, genericArguments: null) }
                    .Concat(EnumerateInterfacesWithArguments(interfaceType));
                foreach (var current in interfaces)
                {
                    var currentType = current.InterfaceType;
                    if (!IsDuckTypeInfrastructureInterface(currentType))
                    {
                        foreach (var method in currentType.Methods)
                        {
                            // Like dynamic duck typing, private (C# 8 helper) and final interface members aren't implemented, nor are
                            // the non-public members of the inherited interfaces (it lists their methods with GetMethods()).
                            if (method.IsConstructor || method.IsStatic || method.IsPrivate || method.IsFinal || (!current.IsProxyType && !method.IsPublic) ||
                                (!includeDuckIgnored && IsDuckIgnoreMethod(method)) ||
                                (implementedAccessors is not null && IsAccessor(method) && (!implementedAccessors.Contains(method) || !addedImplementedAccessors.Add(method))))
                            {
                                continue;
                            }

                            var resolvedMethod = CreateInterfaceMethodForTraversal(method, current.GenericArguments);
                            var key = $"{resolvedMethod.Name}::{resolvedMethod.MethodSig}";
                            if (visitedMethods.Add(key))
                            {
                                results.Add(resolvedMethod);
                                contractsByMethod[resolvedMethod] = new InterfaceMethodContract(
                                    current.InterfaceType,
                                    current.InterfaceReference,
                                    current.InterfaceTypeSig,
                                    current.IsProxyType,
                                    resolvedMethod.MethodSig);
                            }
                        }
                    }
                }

                return new InterfaceMethodCollection(results, contractsByMethod);
            }

            private static IReadOnlyList<TypeSig>? ResolveInterfaceGenericArguments(TypeSig interfaceTypeSig, IReadOnlyList<TypeSig>? inheritedGenericArguments)
            {
                if (interfaceTypeSig is not GenericInstSig genericInstSig || genericInstSig.GenericArguments.Count == 0)
                {
                    return null;
                }

                var genericArguments = new TypeSig[genericInstSig.GenericArguments.Count];
                for (var i = 0; i < genericArguments.Length; i++)
                {
                    genericArguments[i] = SubstituteTypeAndMethodGenericTypeArguments(
                        genericInstSig.GenericArguments[i],
                        inheritedGenericArguments,
                        closedGenericMethodArguments: null);
                }

                return genericArguments;
            }

            private static MethodDef CreateInterfaceMethodForTraversal(MethodDef method, IReadOnlyList<TypeSig>? inheritedGenericArguments)
            {
                if (inheritedGenericArguments is null || inheritedGenericArguments.Count == 0)
                {
                    return method;
                }

                var substitutedSig = CreateSubstitutedMethodSig(method.MethodSig, inheritedGenericArguments);
                if (string.Equals(substitutedSig.ToString(), method.MethodSig.ToString(), StringComparison.Ordinal))
                {
                    return method;
                }

                var copiedMethod = new MethodDefUser(
                    method.Name,
                    substitutedSig,
                    method.ImplAttributes,
                    method.Attributes);

                foreach (var genericParameter in method.GenericParameters)
                {
                    copiedMethod.GenericParameters.Add(new GenericParamUser(genericParameter.Number, genericParameter.Flags, genericParameter.Name)
                    {
                        Kind = genericParameter.Kind
                    });
                }

                foreach (var customAttribute in method.CustomAttributes)
                {
                    copiedMethod.CustomAttributes.Add(customAttribute);
                }

                foreach (var parameterDefinition in method.ParamDefs)
                {
                    var copiedParameterDefinition = new ParamDefUser(parameterDefinition.Name, parameterDefinition.Sequence, parameterDefinition.Attributes);
                    foreach (var customAttribute in parameterDefinition.CustomAttributes)
                    {
                        copiedParameterDefinition.CustomAttributes.Add(customAttribute);
                    }

                    copiedMethod.ParamDefs.Add(copiedParameterDefinition);
                }

                if (TryFindDeclaringPropertyOnType(method.DeclaringType, method, out var property))
                {
                    foreach (var customAttribute in property!.CustomAttributes)
                    {
                        copiedMethod.CustomAttributes.Add(customAttribute);
                    }
                }

                return copiedMethod;
            }

            private static MethodSig CreateSubstitutedMethodSig(MethodSig sourceSig, IReadOnlyList<TypeSig> inheritedGenericArguments)
            {
                var returnType = SubstituteTypeAndMethodGenericTypeArguments(
                    sourceSig.RetType,
                    inheritedGenericArguments,
                    closedGenericMethodArguments: null);
                var parameterTypes = new TypeSig[sourceSig.Params.Count];
                for (var parameterIndex = 0; parameterIndex < parameterTypes.Length; parameterIndex++)
                {
                    parameterTypes[parameterIndex] = SubstituteTypeAndMethodGenericTypeArguments(
                        sourceSig.Params[parameterIndex],
                        inheritedGenericArguments,
                        closedGenericMethodArguments: null);
                }

                var substitutedSig = sourceSig.Generic
                                         ? (sourceSig.HasThis
                                                ? MethodSig.CreateInstanceGeneric(sourceSig.GenParamCount, returnType, parameterTypes)
                                                : MethodSig.CreateStaticGeneric(sourceSig.GenParamCount, returnType, parameterTypes))
                                         : (sourceSig.HasThis
                                                ? MethodSig.CreateInstance(returnType, parameterTypes)
                                                : MethodSig.CreateStatic(returnType, parameterTypes));
                substitutedSig.ExplicitThis = sourceSig.ExplicitThis;
                return substitutedSig;
            }

            private static bool TryFindDeclaringPropertyOnType(TypeDef? declaringType, MethodDef accessorMethod, out PropertyDef? property)
            {
                if (declaringType is not null)
                {
                    foreach (var candidateProperty in declaringType.Properties)
                    {
                        if (AccessorMatches(candidateProperty.GetMethod, accessorMethod) ||
                            AccessorMatches(candidateProperty.SetMethod, accessorMethod))
                        {
                            property = candidateProperty;
                            return true;
                        }
                    }
                }

                property = null;
                return false;
            }

            private static bool IsDuckTypeInfrastructureInterface(TypeDef interfaceType)
                => string.Equals(interfaceType.FullName, "Datadog.Trace.DuckTyping.IDuckType", StringComparison.Ordinal);

            private static IReadOnlyList<MethodDef> BuildClassMethods(TypeDef proxyClassType, bool includeDuckIgnored, HashSet<MethodDef>? implementedAccessors)
            {
                var results = new List<MethodDef>();
                var visitedMethodKeys = new HashSet<string>(StringComparer.Ordinal);
                var visitedSlots = new HashSet<MethodDef>(ReferenceIdentityComparer<MethodDef>.Instance);
                var implementedAccessorIndexes = new Dictionary<string, int>(StringComparer.Ordinal);
                var current = proxyClassType;

                while (current is not null)
                {
                    foreach (var method in current.Methods)
                    {
                        // Like Type.GetMethods(), only the most derived method of a vtable slot counts, even when dynamic duck typing
                        // doesn't implement it (e.g. a sealed override hides the virtual method it overrides).
                        if (method.IsVirtual && !method.IsStatic && !visitedSlots.Add(GetVtableSlotMethod(proxyClassType, method)))
                        {
                            continue;
                        }

                        // Dynamic duck typing binds and implements both accessors of the properties it selects, whatever their
                        // access, even a final one (then it fails to create the proxy type, see TryGetUnloadableProxyTypeFailure).
                        var isImplementedAccessor = implementedAccessors is not null && IsAccessor(method) && implementedAccessors.Contains(method);
                        if (isImplementedAccessor
                                ? method.IsStatic || (!includeDuckIgnored && IsDuckIgnoreMethod(method))
                                : !IsSupportedClassProxyMethod(method, includeDuckIgnored) || (implementedAccessors is not null && IsAccessor(method)))
                        {
                            continue;
                        }

                        var methodTypeArguments = GetClassMethodGenericTypeArguments(proxyClassType, method, null);
                        var key = string.Concat(method.Name, "::", BuildEffectiveMethodSignatureKey(method.MethodSig, methodTypeArguments));
                        if (visitedMethodKeys.Add(key))
                        {
                            if (isImplementedAccessor)
                            {
                                implementedAccessorIndexes[key] = results.Count;
                            }

                            results.Add(method);
                        }
                        else if (isImplementedAccessor && implementedAccessorIndexes.TryGetValue(key, out var index))
                        {
                            // Two properties dynamic duck typing implements (e.g. `new virtual T Value` over `virtual T Value`, both
                            // closed with the same type) get two methods with the same name and signature: the runtime overrides
                            // the most derived method of that signature with the one defined last, for the base type's property.
                            results[index] = method;
                        }
                    }

                    current = current.BaseType?.ResolveTypeDef();
                }

                if (implementedAccessors is not null)
                {
                    // The accessors of the interface properties dynamic duck typing implements too (see BuildImplementedAccessors): the
                    // public method it defines for each one overrides a base virtual method with the same name and signature. A
                    // property of a generic interface implemented twice (IGen<int>, IGen<string>) is selected once, by name, for the
                    // first instance Type.GetInterfaces() lists.
                    var appendedInterfaceAccessors = new HashSet<MethodDef>(ReferenceIdentityComparer<MethodDef>.Instance);
                    foreach (var interfaceEntry in EnumerateInterfacesWithArguments(proxyClassType))
                    {
                        foreach (var method in interfaceEntry.InterfaceType.Methods)
                        {
                            if (!implementedAccessors.Contains(method) || !appendedInterfaceAccessors.Add(method))
                            {
                                continue;
                            }

                            var resolvedMethod = CreateInterfaceMethodForTraversal(method, interfaceEntry.GenericArguments);
                            if (visitedMethodKeys.Add(string.Concat(resolvedMethod.Name, "::", BuildEffectiveMethodSignatureKey(resolvedMethod.MethodSig, closedGenericTargetTypeArguments: null))))
                            {
                                results.Add(resolvedMethod);
                            }
                        }
                    }
                }

                return results;
            }

            /// <summary>
            /// Enumerates the interfaces a type implements like Type.GetInterfaces() lists them (the interface map the runtime
            /// builds): those of the base type first, then each interface the type declares followed by its own interfaces, depth
            /// first in declaration order, skipping the ones already listed. Each one comes with its generic arguments as seen from
            /// the type: a generic interface implemented twice (IGen&lt;int&gt;, IGen&lt;string&gt;) is listed twice.
            /// </summary>
            /// <param name="type">The type.</param>
            /// <returns>The interfaces, with their generic arguments.</returns>
            internal static IEnumerable<InterfaceTraversalEntry> EnumerateInterfacesWithArguments(TypeDef type)
            {
                var hierarchy = new List<TypeDef>();
                for (var current = type; current is not null; current = current.BaseType?.ResolveTypeDef())
                {
                    hierarchy.Add(current);
                }

                var visitedKeys = new HashSet<string>(StringComparer.Ordinal);
                var pending = new Stack<InterfaceTraversalEntry>();
                for (var i = hierarchy.Count - 1; i >= 0; i--)
                {
                    var current = hierarchy[i];
                    PushInterfaces(current, GetClassMemberGenericTypeArguments(type, current, closedRootTypeArguments: null));
                    while (pending.Count > 0)
                    {
                        // The same interface reached through references of other assemblies (e.g. a facade) is the same: the key
                        // doesn't include the assemblies of the generic arguments.
                        var entry = pending.Pop();
                        var key = entry.GenericArguments is null
                                      ? entry.InterfaceType.FullName
                                      : string.Concat(entry.InterfaceType.FullName, "::", string.Join("|", entry.GenericArguments.Select(argument => argument.FullName)));
                        if (!visitedKeys.Add(key))
                        {
                            continue;
                        }

                        yield return entry;
                        PushInterfaces(entry.InterfaceType, entry.GenericArguments);
                    }
                }

                void PushInterfaces(TypeDef declaringType, IReadOnlyList<TypeSig>? typeArguments)
                {
                    // Pushed in reverse, so the interfaces are visited in declaration order.
                    foreach (var interfaceImpl in declaringType.Interfaces.Reverse())
                    {
                        if (ResolveInterfaceTypeDefForTraversal(declaringType, interfaceImpl.Interface) is { } interfaceType)
                        {
                            var interfaceTypeSig = interfaceImpl.Interface.ToTypeSig();
                            pending.Push(new InterfaceTraversalEntry(
                                interfaceType,
                                interfaceImpl.Interface,
                                interfaceTypeSig,
                                isProxyType: false,
                                ResolveInterfaceGenericArguments(interfaceTypeSig, typeArguments)));
                        }
                    }
                }
            }

            /// <summary>
            /// Gets the property accessors a forward proxy implements, like dynamic duck typing: those of the properties
            /// Type.GetProperties() returns for the proxy type, then those of its interfaces whose name isn't taken yet. It doesn't
            /// implement event accessors.
            /// </summary>
            /// <param name="proxyType">The proxy type.</param>
            /// <returns>The accessors.</returns>
            private static HashSet<MethodDef> BuildImplementedAccessors(TypeDef proxyType)
            {
                var accessors = new HashSet<MethodDef>(ReferenceIdentityComparer<MethodDef>.Instance);
                var selectedNames = new HashSet<string>(StringComparer.Ordinal);
                if (proxyType.IsInterface)
                {
                    // All the public properties of the interface itself.
                    foreach (var property in proxyType.Properties)
                    {
                        if (IsPublicProperty(property))
                        {
                            selectedNames.Add(property.Name);
                            AddAccessors(property);
                        }
                    }
                }
                else
                {
                    // Type.GetProperties() of a class, from the most derived type: a property is hidden by a more derived one
                    // using its vtable slot (an override), or with the same name and signature (not substituted: a property of
                    // a generic base type isn't hidden by one of the closed type). Inherited properties whose accessors are all
                    // private don't count. Dynamic duck typing implements the virtual ones.
                    var usedSlots = new HashSet<MethodDef>(ReferenceIdentityComparer<MethodDef>.Instance);
                    var signatures = new HashSet<string>(StringComparer.Ordinal);
                    for (var current = proxyType; current is not null; current = current.BaseType?.ResolveTypeDef())
                    {
                        foreach (var property in current.Properties)
                        {
                            if (!ReferenceEquals(current, proxyType) && property.GetMethod?.IsPrivate != false && property.SetMethod?.IsPrivate != false)
                            {
                                continue;
                            }

                            var slotAccessor = property.GetMethod is { IsPublic: true } publicGetter ? publicGetter : property.SetMethod is { IsPublic: true } publicSetter ? publicSetter : null;
                            if (slotAccessor is { IsVirtual: true } && !usedSlots.Add(GetVtableSlotMethod(proxyType, slotAccessor)))
                            {
                                continue;
                            }

                            var signature = property.PropertySig;
                            if (!signatures.Add(string.Concat(property.Name, "|", signature?.HasThis == true ? "instance" : "static", "|", string.Join(",", signature is null ? [] : new[] { signature.RetType }.Concat(signature.Params).Select(GetRawSignatureTypeName)))))
                            {
                                continue;
                            }

                            if (IsPublicProperty(property) && (property.GetMethod?.IsVirtual == true || property.SetMethod?.IsVirtual == true))
                            {
                                selectedNames.Add(property.Name);
                                AddAccessors(property, inherited: !ReferenceEquals(current, proxyType));
                            }
                        }
                    }
                }

                // Then the properties of the interfaces, in Type.GetInterfaces() order, whose name isn't taken (AddInterfaceProperties).
                foreach (var interfaceType in EnumerateInterfacesForTraversal(proxyType))
                {
                    foreach (var property in interfaceType.Properties)
                    {
                        if (IsPublicProperty(property) && selectedNames.Add(property.Name))
                        {
                            AddAccessors(property);
                        }
                    }
                }

                return accessors;

                static bool IsPublicProperty(PropertyDef property) => property.GetMethod?.IsPublic == true || property.SetMethod?.IsPublic == true;

                // Like the signature comparison of reflection: generic parameters by position, the other types by name (whatever
                // the assembly reference spelling them).
                static string GetRawSignatureTypeName(TypeSig? type) => type switch
                {
                    null => string.Empty,
                    GenericSig genericSig => string.Concat(genericSig.IsMethodVar ? "!!" : "!", genericSig.Number.ToString(CultureInfo.InvariantCulture)),
                    GenericInstSig genericInstSig => string.Concat(genericInstSig.GenericType?.FullName, "<", string.Join(",", genericInstSig.GenericArguments.Select(GetRawSignatureTypeName)), ">"),
                    ArraySig arraySig => string.Concat(GetRawSignatureTypeName(arraySig.Next), "[", arraySig.Rank.ToString(CultureInfo.InvariantCulture), "]"),
                    ModifierSig modifierSig => string.Concat(GetRawSignatureTypeName(modifierSig.Next), modifierSig is CModReqdSig ? " modreq(" : " modopt(", modifierSig.Modifier?.FullName, ")"),
                    NonLeafSig nonLeafSig => string.Concat(nonLeafSig.ElementType.ToString(), "(", GetRawSignatureTypeName(nonLeafSig.Next), ")"),
                    _ => type.FullName,
                };

                // Reflection doesn't see the private accessors of an inherited property (it can't be read or written through them).
                void AddAccessors(PropertyDef property, bool inherited = false)
                {
                    if (property.GetMethod is { } getter && !(inherited && getter.IsPrivate))
                    {
                        accessors.Add(getter);
                    }

                    if (property.SetMethod is { } setter && !(inherited && setter.IsPrivate))
                    {
                        accessors.Add(setter);
                    }
                }
            }

            /// <summary>
            /// Enumerates the interface types a type implements like Type.GetInterfaces() lists them, each generic interface
            /// once (see EnumerateInterfacesWithArguments).
            /// </summary>
            /// <param name="type">The type.</param>
            /// <returns>The interfaces.</returns>
            private static IEnumerable<TypeDef> EnumerateInterfacesForTraversal(TypeDef type)
            {
                var visitedTypes = new HashSet<TypeDef>();
                foreach (var entry in EnumerateInterfacesWithArguments(type))
                {
                    if (visitedTypes.Add(entry.InterfaceType))
                    {
                        yield return entry.InterfaceType;
                    }
                }
            }

            internal static bool IsAccessor(MethodDef method)
                => method.IsSpecialName && (method.IsGetter || method.IsSetter || method.IsAddOn || method.IsRemoveOn || method.IsFire);

            private static MethodDef? BuildSupportedBaseConstructor(TypeDef proxyType)
            {
                foreach (var constructor in proxyType.Methods)
                {
                    if (!constructor.IsConstructor || constructor.IsStatic || constructor.MethodSig.Params.Count != 0)
                    {
                        continue;
                    }

                    return constructor;
                }

                return null;
            }

            private static IReadOnlyDictionary<MethodDef, PropertyDef> BuildAccessorPropertyMap(TypeDef proxyType)
            {
                var propertiesByAccessorMethod = new Dictionary<MethodDef, PropertyDef>(ReferenceIdentityComparer<MethodDef>.Instance);
                var visitedTypes = new HashSet<string>(StringComparer.Ordinal);
                var typesToInspect = new Stack<TypeDef>();
                typesToInspect.Push(proxyType);

                while (typesToInspect.Count > 0)
                {
                    var currentType = typesToInspect.Pop();
                    if (!visitedTypes.Add(currentType.FullName))
                    {
                        continue;
                    }

                    foreach (var property in currentType.Properties)
                    {
                        if (property.GetMethod is not null && !propertiesByAccessorMethod.ContainsKey(property.GetMethod))
                        {
                            propertiesByAccessorMethod[property.GetMethod] = property;
                        }

                        if (property.SetMethod is not null && !propertiesByAccessorMethod.ContainsKey(property.SetMethod))
                        {
                            propertiesByAccessorMethod[property.SetMethod] = property;
                        }
                    }

                    var baseType = currentType.BaseType?.ResolveTypeDef();
                    if (baseType is not null)
                    {
                        typesToInspect.Push(baseType);
                    }

                    foreach (var interfaceImpl in currentType.Interfaces)
                    {
                        var resolvedInterface = ResolveInterfaceTypeDefForTraversal(currentType, interfaceImpl.Interface);
                        if (resolvedInterface is not null)
                        {
                            typesToInspect.Push(resolvedInterface);
                        }
                    }
                }

                return propertiesByAccessorMethod;
            }
        }

        private sealed class ProxyMethodPlan
        {
            internal ProxyMethodPlan(MethodDef proxyMethod)
            {
                Method = proxyMethod;
                IdentityKey = BuildMethodIdentityKey(proxyMethod);
                DeclaringProperty = FindPropertyFromAccessor(proxyMethod);
                FieldResolutionMode = GetFieldResolutionMode(proxyMethod);
                AllowPrivateBaseMembers = IsFallbackToBaseTypesEnabled(proxyMethod);
                AllowPrivateBaseMethodCandidates = AllowPrivateBaseMembers && IsPropertyAccessorMethod(proxyMethod);
                DuckBindingFlags = GetDuckBindingFlags(proxyMethod);
                DuckBindingFlagsCacheKey = ((int)DuckBindingFlags).ToString(CultureInfo.InvariantCulture);
                UseIgnoreCaseMemberMatching = (DuckBindingFlags & BindingFlags.IgnoreCase) != 0;
                ForwardTargetMethodNames = GetForwardTargetMethodNames(proxyMethod);
                ForwardTargetMethodNamesCacheKey = string.Join("|", ForwardTargetMethodNames);
                ForwardTargetFieldNames = GetForwardTargetFieldNames(proxyMethod);
                HasFieldAccessorKind = TryGetFieldAccessorKind(proxyMethod, out var accessorKind);
                FieldAccessorKind = accessorKind;
                ReverseUsageFailureDetail = TryGetForwardReverseUsageFailure(proxyMethod, out var reverseUsageFailureDetail)
                                                ? reverseUsageFailureDetail
                                                : null;
                HasExplicitInterfaceTypeNames = TryGetForwardExplicitInterfaceTypeNames(proxyMethod, out var explicitInterfaceTypeNames, out var useRelaxedNameComparison);
                ExplicitInterfaceTypeNames = explicitInterfaceTypeNames;
                ExplicitInterfaceTypeNamesCacheKey = string.Join("|", ExplicitInterfaceTypeNames);
                UseRelaxedNameComparison = useRelaxedNameComparison;
                HasConfiguredParameterTypeNames = TryGetForwardParameterTypeNames(proxyMethod, out var configuredParameterTypeNames);
                ConfiguredParameterTypeNames = configuredParameterTypeNames;
                ConfiguredParameterTypeNamesCacheKey = string.Join("|", ConfiguredParameterTypeNames);
                HasDuckGenericParameterTypeNames = TryGetDuckGenericParameterTypeNames(proxyMethod, out var genericParameterTypeNames);
                DuckGenericParameterTypeNames = genericParameterTypeNames;
                SetterTargetPropertyNames = BuildSetterTargetPropertyNames(ForwardTargetMethodNames);
                ParameterDirections = BuildParameterDirections(proxyMethod);
                ParameterCountCacheKey = proxyMethod.MethodSig.Params.Count.ToString(CultureInfo.InvariantCulture);
            }

            internal MethodDef Method { get; }

            internal string IdentityKey { get; }

            internal PropertyDef? DeclaringProperty { get; }

            internal FieldResolutionMode FieldResolutionMode { get; }

            internal bool AllowPrivateBaseMembers { get; }

            internal bool AllowPrivateBaseMethodCandidates { get; }

            internal bool UseIgnoreCaseMemberMatching { get; }

            internal BindingFlags DuckBindingFlags { get; }

            internal string DuckBindingFlagsCacheKey { get; }

            internal IReadOnlyList<string> ForwardTargetMethodNames { get; }

            internal string ForwardTargetMethodNamesCacheKey { get; }

            internal IReadOnlyList<string> ForwardTargetFieldNames { get; }

            internal bool HasFieldAccessorKind { get; }

            internal FieldAccessorKind FieldAccessorKind { get; }

            internal string? ReverseUsageFailureDetail { get; }

            internal bool HasExplicitInterfaceTypeNames { get; }

            internal IReadOnlyList<string> ExplicitInterfaceTypeNames { get; }

            internal string ExplicitInterfaceTypeNamesCacheKey { get; }

            internal bool UseRelaxedNameComparison { get; }

            internal bool HasConfiguredParameterTypeNames { get; }

            internal IReadOnlyList<string> ConfiguredParameterTypeNames { get; }

            internal string ConfiguredParameterTypeNamesCacheKey { get; }

            internal bool HasDuckGenericParameterTypeNames { get; }

            internal IReadOnlyList<string> DuckGenericParameterTypeNames { get; }

            internal IReadOnlyList<string> SetterTargetPropertyNames { get; }

            internal IReadOnlyList<ParameterDirection> ParameterDirections { get; }

            internal string ParameterCountCacheKey { get; }

            internal bool TryGetParameterDirection(int parameterIndex, out ParameterDirection direction)
            {
                if (parameterIndex < 0 || parameterIndex >= ParameterDirections.Count)
                {
                    direction = default;
                    return false;
                }

                direction = ParameterDirections[parameterIndex];
                return true;
            }

            private static IReadOnlyList<ParameterDirection> BuildParameterDirections(MethodDef method)
            {
                if (method.MethodSig.Params.Count == 0)
                {
                    return Array.Empty<ParameterDirection>();
                }

                var directions = new ParameterDirection[method.MethodSig.Params.Count];
                for (var parameterIndex = 0; parameterIndex < directions.Length; parameterIndex++)
                {
                    _ = TryGetMethodParameterDirection(method, parameterIndex, out directions[parameterIndex]);
                }

                return directions;
            }

            private static IReadOnlyList<string> BuildSetterTargetPropertyNames(IReadOnlyList<string> forwardTargetMethodNames)
            {
                if (forwardTargetMethodNames.Count == 0)
                {
                    return Array.Empty<string>();
                }

                var propertyNames = new List<string>(forwardTargetMethodNames.Count);
                var seenPropertyNames = new HashSet<string>(StringComparer.Ordinal);
                for (var i = 0; i < forwardTargetMethodNames.Count; i++)
                {
                    var propertyName = ExtractSetterPropertyName(forwardTargetMethodNames[i]);
                    if (StringUtil.IsNullOrWhiteSpace(propertyName))
                    {
                        continue;
                    }

                    var nonNullPropertyName = propertyName!;
                    if (!seenPropertyNames.Add(nonNullPropertyName))
                    {
                        continue;
                    }

                    propertyNames.Add(nonNullPropertyName);
                }

                return propertyNames;
            }
        }

        private sealed class MethodPlan
        {
            internal MethodPlan(MethodDef method)
            {
                Method = method;
                IdentityKey = BuildMethodIdentityKey(method);
                ParameterDirections = BuildParameterDirections(method);
            }

            internal MethodDef Method { get; }

            internal string IdentityKey { get; }

            internal IReadOnlyList<ParameterDirection> ParameterDirections { get; }

            internal bool TryGetParameterDirection(int parameterIndex, out ParameterDirection direction)
            {
                if (parameterIndex < 0 || parameterIndex >= ParameterDirections.Count)
                {
                    direction = default;
                    return false;
                }

                direction = ParameterDirections[parameterIndex];
                return true;
            }

            private static IReadOnlyList<ParameterDirection> BuildParameterDirections(MethodDef method)
            {
                if (method.MethodSig.Params.Count == 0)
                {
                    return Array.Empty<ParameterDirection>();
                }

                var directions = new ParameterDirection[method.MethodSig.Params.Count];
                for (var parameterIndex = 0; parameterIndex < directions.Length; parameterIndex++)
                {
                    _ = TryGetMethodParameterDirection(method, parameterIndex, out directions[parameterIndex]);
                }

                return directions;
            }
        }

        private sealed partial class EmitterExecutionContext
        {
            private readonly Dictionary<TypeDef, TargetTypePlan> _targetTypePlans = new();
            private readonly Dictionary<TypeDef, ProxyTypePlan> _proxyTypePlans = new();
            private readonly Dictionary<MethodDef, MethodPlan> _methodPlans = new(ReferenceIdentityComparer<MethodDef>.Instance);
            private readonly Dictionary<string, ForwardMethodBindingPlanCacheEntry> _forwardMethodBindingsByKey = new(StringComparer.Ordinal);
            private readonly Dictionary<string, MethodReturnConversionCacheEntry> _methodReturnConversionsByKey = new(StringComparer.Ordinal);
            private readonly Dictionary<string, MethodArgumentConversionCacheEntry> _methodArgumentConversionsByKey = new(StringComparer.Ordinal);

            internal TargetTypePlan GetOrCreateTargetTypePlan(TypeDef targetType)
            {
                if (_targetTypePlans.TryGetValue(targetType, out var cachedPlan))
                {
                    return cachedPlan;
                }

                var plan = new TargetTypePlan(targetType);
                _targetTypePlans[targetType] = plan;
                return plan;
            }

            internal ProxyTypePlan GetOrCreateProxyTypePlan(TypeDef proxyType)
            {
                if (_proxyTypePlans.TryGetValue(proxyType, out var cachedPlan))
                {
                    return cachedPlan;
                }

                var plan = new ProxyTypePlan(proxyType);
                _proxyTypePlans[proxyType] = plan;
                return plan;
            }

            internal MethodPlan GetOrCreateMethodPlan(MethodDef method)
            {
                if (_methodPlans.TryGetValue(method, out var cachedPlan))
                {
                    return cachedPlan;
                }

                var plan = new MethodPlan(method);
                _methodPlans[method] = plan;
                return plan;
            }

            internal bool TryGetForwardMethodBindingPlan(string key, out ForwardMethodBindingPlanCacheEntry plan)
                => _forwardMethodBindingsByKey.TryGetValue(key, out plan!);

            internal void CacheForwardMethodBindingPlan(string key, ForwardMethodBindingPlanCacheEntry plan)
                => _forwardMethodBindingsByKey[key] = plan;

            internal bool TryGetMethodReturnConversion(string key, out MethodReturnConversionCacheEntry conversion)
                => _methodReturnConversionsByKey.TryGetValue(key, out conversion!);

            internal void CacheMethodReturnConversion(string key, MethodReturnConversionCacheEntry conversion)
                => _methodReturnConversionsByKey[key] = conversion;

            internal bool TryGetMethodArgumentConversion(string key, out MethodArgumentConversionCacheEntry conversion)
                => _methodArgumentConversionsByKey.TryGetValue(key, out conversion!);

            internal void CacheMethodArgumentConversion(string key, MethodArgumentConversionCacheEntry conversion)
                => _methodArgumentConversionsByKey[key] = conversion;
        }
    }
}
