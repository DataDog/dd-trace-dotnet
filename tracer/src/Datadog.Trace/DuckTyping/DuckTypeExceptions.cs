// <copyright file="DuckTypeExceptions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using Datadog.Trace.Util;
#pragma warning disable SA1649 // File name must match first type name
#pragma warning disable SA1402 // File may only contain a single class

namespace Datadog.Trace.DuckTyping
{
    /// <summary>
    /// DuckType Exception
    /// </summary>
    internal class DuckTypeException : Exception
    {
        protected DuckTypeException(string message)
            : base(message)
        {
        }

        protected DuckTypeException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        internal static DuckTypeException Create(string message) => new(message);

        internal static DuckTypeException Create(string message, Exception innerException) => new(message, innerException);

        [DebuggerHidden]
        [DoesNotReturn]
        internal static void Throw(string message) => throw Create(message);

#if !NET6_0_OR_GREATER
        internal DuckTypeException CloneForThrow() => (DuckTypeException)MemberwiseClone();
#endif
    }

    /// <summary>
    /// DuckType proxy type definition is null
    /// </summary>
    internal sealed class DuckTypeProxyTypeDefinitionIsNull : DuckTypeException
    {
        private DuckTypeProxyTypeDefinitionIsNull()
            : base($"The proxy type definition is null.")
        {
        }

        internal static DuckTypeProxyTypeDefinitionIsNull Create() => new();

        [DebuggerHidden]
        [DoesNotReturn]
        internal static void Throw() => throw Create();
    }

    /// <summary>
    /// DuckType target object instance is null
    /// </summary>
    internal sealed class DuckTypeTargetObjectInstanceIsNull : DuckTypeException
    {
        private DuckTypeTargetObjectInstanceIsNull()
            : base($"The target object instance is null.")
        {
        }

        internal static DuckTypeTargetObjectInstanceIsNull Create() => new();

        [DebuggerHidden]
        [DoesNotReturn]
        internal static void Throw() => throw Create();
    }

    /// <summary>
    /// DuckType invalid type conversion exception
    /// </summary>
    internal sealed class DuckTypeInvalidTypeConversionException : DuckTypeException
    {
        private DuckTypeInvalidTypeConversionException(Type actualType, Type expectedType)
            : base($"Invalid type conversion from {actualType.FullName} to {expectedType.FullName}")
        {
        }

        private DuckTypeInvalidTypeConversionException(string message, bool useRawMessage)
            : base(message)
        {
        }

        internal static DuckTypeInvalidTypeConversionException Create(Type actualType, Type expectedType) => new(actualType, expectedType);

        internal static Exception CreateForAot(string message) => new DuckTypeInvalidTypeConversionException(message, true);
    }

    /// <summary>
    /// DuckType property can't be read
    /// </summary>
    internal sealed class DuckTypePropertyCantBeReadException : DuckTypeException
    {
        private DuckTypePropertyCantBeReadException(PropertyInfo property)
            : base($"The property '{property.Name}' can't be read, you should remove the getter from the proxy definition base type class or interface.")
        {
        }

        private DuckTypePropertyCantBeReadException(string message, bool useRawMessage)
            : base(message)
        {
        }

        internal static DuckTypePropertyCantBeReadException Create(PropertyInfo property) => new(property);

        internal static Exception CreateForAot(string message) => new DuckTypePropertyCantBeReadException(message, true);
    }

    /// <summary>
    /// DuckType property can't be written
    /// </summary>
    internal sealed class DuckTypePropertyCantBeWrittenException : DuckTypeException
    {
        private DuckTypePropertyCantBeWrittenException(PropertyInfo property)
            : base($"The property '{property.Name}' can't be written, you should remove the setter from the proxy definition base type class or interface.")
        {
        }

        private DuckTypePropertyCantBeWrittenException(string message, bool useRawMessage)
            : base(message)
        {
        }

        internal static DuckTypePropertyCantBeWrittenException Create(PropertyInfo property) => new(property);

        internal static Exception CreateForAot(string message) => new DuckTypePropertyCantBeWrittenException(message, true);
    }

    /// <summary>
    /// DuckType property argument doesn't have the same argument length
    /// </summary>
    internal sealed class DuckTypePropertyArgumentsLengthException : DuckTypeException
    {
        private DuckTypePropertyArgumentsLengthException(PropertyInfo property)
            : base($"The property '{property.Name}' doesn't have the same number of arguments as the original property.")
        {
        }

        private DuckTypePropertyArgumentsLengthException(string message, bool useRawMessage)
            : base(message)
        {
        }

        internal static DuckTypePropertyArgumentsLengthException Create(PropertyInfo property) => new(property);

        internal static Exception CreateForAot(string message) => new DuckTypePropertyArgumentsLengthException(message, true);
    }

    /// <summary>
    /// DuckType field is readonly
    /// </summary>
    internal sealed class DuckTypeFieldIsReadonlyException : DuckTypeException
    {
        private DuckTypeFieldIsReadonlyException(FieldInfo field)
            : base($"The field '{field.Name}' is marked as readonly, you should remove the setter from the base type class or interface.")
        {
        }

        private DuckTypeFieldIsReadonlyException(string message, bool useRawMessage)
            : base(message)
        {
        }

        internal static DuckTypeFieldIsReadonlyException Create(FieldInfo field) => new(field);

        internal static Exception CreateForAot(string message) => new DuckTypeFieldIsReadonlyException(message, true);
    }

    /// <summary>
    /// DuckType property or field not found
    /// </summary>
    internal sealed class DuckTypePropertyOrFieldNotFoundException : DuckTypeException
    {
        private DuckTypePropertyOrFieldNotFoundException(string name, string duckAttributeName, string type)
            : base($"The property or field '{duckAttributeName}' for the proxy property '{name}' was not found in the instance of type '{type}'.")
        {
        }

        private DuckTypePropertyOrFieldNotFoundException(string message, bool useRawMessage)
            : base(message)
        {
        }

        internal static DuckTypePropertyOrFieldNotFoundException Create(string name, string duckAttributeName, Type type)
            => new(name, duckAttributeName, type?.FullName ?? type?.Name ?? "NULL");

        internal static Exception CreateForAot(string message) => new DuckTypePropertyOrFieldNotFoundException(message, true);
    }

    /// <summary>
    /// DuckType struct members cannot be changed exception
    /// </summary>
    internal sealed class DuckTypeStructMembersCannotBeChangedException : DuckTypeException
    {
        private DuckTypeStructMembersCannotBeChangedException(Type type)
            : base($"Modifying struct members is not supported. [{type.FullName}]")
        {
        }

        private DuckTypeStructMembersCannotBeChangedException(string message, bool useRawMessage)
            : base(message)
        {
        }

        internal static DuckTypeStructMembersCannotBeChangedException Create(Type type) => new(type);

        internal static Exception CreateForAot(string message) => new DuckTypeStructMembersCannotBeChangedException(message, true);
    }

    /// <summary>
    /// DuckType target method can not be found exception
    /// </summary>
    internal sealed class DuckTypeTargetMethodNotFoundException : DuckTypeException
    {
        private DuckTypeTargetMethodNotFoundException(MethodInfo method)
            : base($"The target method for the proxy method '{method}' was not found.")
        {
        }

        private DuckTypeTargetMethodNotFoundException(string message, bool useRawMessage)
            : base(message)
        {
        }

        internal static DuckTypeTargetMethodNotFoundException Create(MethodInfo method) => new(method);

        internal static Exception CreateForAot(string message) => new DuckTypeTargetMethodNotFoundException(message, true);
    }

    /// <summary>
    /// DuckType proxy method parameter is missing exception
    /// </summary>
    internal sealed class DuckTypeProxyMethodParameterIsMissingException : DuckTypeException
    {
        private DuckTypeProxyMethodParameterIsMissingException(MethodInfo proxyMethod, ParameterInfo targetParameterInfo)
            : base($"The proxy method '{proxyMethod.Name}' is missing parameter '{targetParameterInfo.Name}' declared in the target method.")
        {
        }

        private DuckTypeProxyMethodParameterIsMissingException(string message, bool useRawMessage)
            : base(message)
        {
        }

        internal static DuckTypeProxyMethodParameterIsMissingException Create(MethodInfo proxyMethod, ParameterInfo targetParameterInfo)
            => new(proxyMethod, targetParameterInfo);

        internal static Exception CreateForAot(string message) => new DuckTypeProxyMethodParameterIsMissingException(message, true);
    }

    /// <summary>
    /// DuckType parameter signature mismatch between proxy and target method
    /// </summary>
    internal sealed class DuckTypeProxyAndTargetMethodParameterSignatureMismatchException : DuckTypeException
    {
        private DuckTypeProxyAndTargetMethodParameterSignatureMismatchException(MethodInfo proxyMethod, MethodInfo targetMethod)
            : base($"Parameter signature mismatch between proxy '{proxyMethod}' and target method '{targetMethod}'")
        {
        }

        private DuckTypeProxyAndTargetMethodParameterSignatureMismatchException(string message, bool useRawMessage)
            : base(message)
        {
        }

        internal static DuckTypeProxyAndTargetMethodParameterSignatureMismatchException Create(MethodInfo proxyMethod, MethodInfo targetMethod) => new(proxyMethod, targetMethod);

        internal static Exception CreateForAot(string message) => new DuckTypeProxyAndTargetMethodParameterSignatureMismatchException(message, true);
    }

    /// <summary>
    /// DuckType parameter signature mismatch between proxy and target method
    /// </summary>
    internal sealed class DuckTypeProxyAndTargetMethodReturnTypeMismatchException : DuckTypeException
    {
        private DuckTypeProxyAndTargetMethodReturnTypeMismatchException(MethodInfo proxyMethod, MethodInfo targetMethod)
            : base($"Return type mismatch between proxy '{proxyMethod}' and target method '{targetMethod}'.")
        {
        }

        private DuckTypeProxyAndTargetMethodReturnTypeMismatchException(string message, bool useRawMessage)
            : base(message)
        {
        }

        internal static DuckTypeProxyAndTargetMethodReturnTypeMismatchException Create(MethodInfo proxyMethod, MethodInfo targetMethod) => new(proxyMethod, targetMethod);

        internal static Exception CreateForAot(string message) => new DuckTypeProxyAndTargetMethodReturnTypeMismatchException(message, true);
    }

    /// <summary>
    /// DuckType proxy methods with generic parameters are not supported in non public instances exception
    /// </summary>
    internal sealed class DuckTypeProxyMethodsWithGenericParametersNotSupportedInNonPublicInstancesException : DuckTypeException
    {
        private DuckTypeProxyMethodsWithGenericParametersNotSupportedInNonPublicInstancesException(MethodInfo proxyMethod)
            : base($"The proxy method with generic parameters '{proxyMethod}' are not supported on non public instances")
        {
        }

        private DuckTypeProxyMethodsWithGenericParametersNotSupportedInNonPublicInstancesException(string message, bool useRawMessage)
            : base(message)
        {
        }

        internal static DuckTypeProxyMethodsWithGenericParametersNotSupportedInNonPublicInstancesException Create(MethodInfo proxyMethod) => new(proxyMethod);

        internal static Exception CreateForAot(string message) => new DuckTypeProxyMethodsWithGenericParametersNotSupportedInNonPublicInstancesException(message, true);
    }

    /// <summary>
    /// DuckType proxy method has an ambiguous match in the target type exception
    /// </summary>
    internal sealed class DuckTypeTargetMethodAmbiguousMatchException : DuckTypeException
    {
        private DuckTypeTargetMethodAmbiguousMatchException(MethodInfo proxyMethod, MethodInfo targetMethod, MethodInfo targetMethod2)
            : base($"The proxy method '{proxyMethod}' matches at least two methods in the target type. Method1 = '{targetMethod}' and Method2 = '{targetMethod2}'")
        {
        }

        private DuckTypeTargetMethodAmbiguousMatchException(string message, bool useRawMessage)
            : base(message)
        {
        }

        internal static DuckTypeTargetMethodAmbiguousMatchException Create(MethodInfo proxyMethod, MethodInfo targetMethod, MethodInfo targetMethod2) => new(proxyMethod, targetMethod, targetMethod2);

        internal static Exception CreateForAot(string message) => new DuckTypeTargetMethodAmbiguousMatchException(message, true);
    }

    /// <summary>
    /// DuckType proxy property has an ambiguous match in the target type exception
    /// </summary>
    internal sealed class DuckTypeTargetPropertyAmbiguousMatchException : DuckTypeException
    {
        private DuckTypeTargetPropertyAmbiguousMatchException(Type targetType, string propertyName)
            : base($"The target type '{targetType.FullName ?? targetType.Name}' declares more than one property called '{propertyName}', so the one to copy from cannot be determined.")
        {
        }

        private DuckTypeTargetPropertyAmbiguousMatchException(string message, bool useRawMessage)
            : base(message)
        {
        }

        internal static DuckTypeTargetPropertyAmbiguousMatchException Create(Type targetType, string propertyName) => new(targetType, propertyName);

        internal static Exception CreateForAot(string message) => new DuckTypeTargetPropertyAmbiguousMatchException(message, true);
    }

    /// <summary>
    /// DuckType reverse proxy type to derive from is a struct exception
    /// </summary>
    internal sealed class DuckTypeReverseProxyBaseIsStructException : DuckTypeException
    {
        private DuckTypeReverseProxyBaseIsStructException(Type type)
            : base($"Cannot derive from struct type '{type.FullName}' for reverse proxy")
        {
        }

        private DuckTypeReverseProxyBaseIsStructException(string message, bool useRawMessage)
            : base(message)
        {
        }

        internal static DuckTypeReverseProxyBaseIsStructException Create(Type type) => new(type);

        internal static Exception CreateForAot(string message) => new DuckTypeReverseProxyBaseIsStructException(message, true);
    }

    /// <summary>
    /// DuckType proxy method is abstract
    /// </summary>
    internal sealed class DuckTypeReverseProxyImplementorIsAbstractOrInterfaceException : DuckTypeException
    {
        private DuckTypeReverseProxyImplementorIsAbstractOrInterfaceException(Type type)
            : base($"The implementation type '{type.FullName}' must not be an interface or abstract type for reverse proxy")
        {
        }

        private DuckTypeReverseProxyImplementorIsAbstractOrInterfaceException(string message, bool useRawMessage)
            : base(message)
        {
        }

        internal static DuckTypeReverseProxyImplementorIsAbstractOrInterfaceException Create(Type type) => new(type);

        internal static Exception CreateForAot(string message) => new DuckTypeReverseProxyImplementorIsAbstractOrInterfaceException(message, true);
    }

    /// <summary>
    /// DuckType property can't be read
    /// </summary>
    internal sealed class DuckTypeReverseProxyPropertyCannotBeAbstractException : DuckTypeException
    {
        private DuckTypeReverseProxyPropertyCannotBeAbstractException(PropertyInfo property)
            : base($"The property '{property.Name}' cannot be abstract for reverse proxy")
        {
        }

        private DuckTypeReverseProxyPropertyCannotBeAbstractException(string message, bool useRawMessage)
            : base(message)
        {
        }

        internal static DuckTypeReverseProxyPropertyCannotBeAbstractException Create(PropertyInfo property) => new(property);

        internal static Exception CreateForAot(string message) => new DuckTypeReverseProxyPropertyCannotBeAbstractException(message, true);
    }

    /// <summary>
    /// DuckType method was [DuckReverseMethod] in non-reverse proxy
    /// </summary>
    internal sealed class DuckTypeIncorrectReverseMethodUsageException : DuckTypeException
    {
        private DuckTypeIncorrectReverseMethodUsageException(MethodInfo method)
            : base($"The method '{method.Name}' was marked as a [DuckReverseMethod] but not doing reverse duck typing.")
        {
        }

        private DuckTypeIncorrectReverseMethodUsageException(string message, bool useRawMessage)
            : base(message)
        {
        }

        internal static DuckTypeIncorrectReverseMethodUsageException Create(MethodInfo method) => new(method);

        internal static Exception CreateForAot(string message) => new DuckTypeIncorrectReverseMethodUsageException(message, true);
    }

    /// <summary>
    /// DuckType property was [DuckReverseMethod] in non-reverse proxy
    /// </summary>
    internal sealed class DuckTypeIncorrectReversePropertyUsageException : DuckTypeException
    {
        private DuckTypeIncorrectReversePropertyUsageException(PropertyInfo property)
            : base($"The property '{property.Name}' was marked as a [DuckReverseMethod] but not doing reverse duck typing.")
        {
        }

        private DuckTypeIncorrectReversePropertyUsageException(string message, bool useRawMessage)
            : base(message)
        {
        }

        internal static DuckTypeIncorrectReversePropertyUsageException Create(PropertyInfo property) => new(property);

        internal static Exception CreateForAot(string message) => new DuckTypeIncorrectReversePropertyUsageException(message, true);
    }

    /// <summary>
    /// DuckType proxy was missing an implementation
    /// </summary>
    internal sealed class DuckTypeReverseProxyMissingPropertyImplementationException : DuckTypeException
    {
        private DuckTypeReverseProxyMissingPropertyImplementationException(IEnumerable<PropertyInfo> properties)
            : base($"The duck reverse proxy was missing implementations for properties: {string.Join(", ", properties.Select(x => x.Name))}")
        {
        }

        private DuckTypeReverseProxyMissingPropertyImplementationException(string message, bool useRawMessage)
            : base(message)
        {
        }

        internal static DuckTypeReverseProxyMissingPropertyImplementationException Create(IEnumerable<PropertyInfo> properties) => new(properties);

        internal static Exception CreateForAot(string message) => new DuckTypeReverseProxyMissingPropertyImplementationException(message, true);
    }

    /// <summary>
    /// DuckType proxy was missing an implementation
    /// </summary>
    internal sealed class DuckTypeReverseProxyMissingMethodImplementationException : DuckTypeException
    {
        private DuckTypeReverseProxyMissingMethodImplementationException(IEnumerable<MethodInfo> methods)
            : base($"The duck reverse proxy was missing implementations for methods: {string.Join(", ", methods.Select(x => x.Name))}")
        {
        }

        private DuckTypeReverseProxyMissingMethodImplementationException(string message, bool useRawMessage)
            : base(message)
        {
        }

        internal static DuckTypeReverseProxyMissingMethodImplementationException Create(IEnumerable<MethodInfo> methods) => new(methods);

        internal static Exception CreateForAot(string message) => new DuckTypeReverseProxyMissingMethodImplementationException(message, true);
    }

    /// <summary>
    /// DuckType proxy tried to implement a generic method in a non-generic way
    /// </summary>
    internal sealed class DuckTypeReverseAttributeParameterNamesMismatchException : DuckTypeException
    {
        private DuckTypeReverseAttributeParameterNamesMismatchException(MethodInfo method)
            : base($"The reverse duck attribute parameter names for method '{method.Name}' did not match the method's parameters ")
        {
        }

        private DuckTypeReverseAttributeParameterNamesMismatchException(string message, bool useRawMessage)
            : base(message)
        {
        }

        internal static DuckTypeReverseAttributeParameterNamesMismatchException Create(MethodInfo method) => new(method);

        internal static Exception CreateForAot(string message) => new DuckTypeReverseAttributeParameterNamesMismatchException(message, true);
    }

    /// <summary>
    /// DuckType proxy tried to implement a generic method in a non-generic way
    /// </summary>
    internal sealed class DuckTypeReverseProxyMustImplementGenericMethodAsGenericException : DuckTypeException
    {
        private DuckTypeReverseProxyMustImplementGenericMethodAsGenericException(MethodInfo implementationMethod, MethodInfo targetMethod)
            : base($"The duck reverse proxy implementation '{implementationMethod.Name}' for generic target method '{targetMethod.Name}' " +
                   $"must have same number of generic parameters - had {implementationMethod.GetGenericArguments().Length}, expected {targetMethod.GetGenericArguments().Length}")
        {
        }

        private DuckTypeReverseProxyMustImplementGenericMethodAsGenericException(string message, bool useRawMessage)
            : base(message)
        {
        }

        internal static DuckTypeReverseProxyMustImplementGenericMethodAsGenericException Create(MethodInfo implementationMethod, MethodInfo targetMethod) => new(implementationMethod, targetMethod);

        internal static Exception CreateForAot(string message) => new DuckTypeReverseProxyMustImplementGenericMethodAsGenericException(message, true);
    }

    /// <summary>
    /// DuckType property or field not found
    /// </summary>
    internal sealed class DuckTypeCustomAttributeHasNamedArgumentsException : DuckTypeException
    {
        private DuckTypeCustomAttributeHasNamedArgumentsException(string attributeName, string type)
            : base($"The attribute '{attributeName}' applied to '{type}' uses named arguments. Named arguments are not supported for custom attributes.")
        {
        }

        private DuckTypeCustomAttributeHasNamedArgumentsException(string message, bool useRawMessage)
            : base(message)
        {
        }

        internal static DuckTypeCustomAttributeHasNamedArgumentsException Create(Type type, CustomAttributeData attributeData) => new(attributeData.AttributeType?.FullName ?? "Null", type?.FullName ?? type?.Name ?? "NULL");

        internal static Exception CreateForAot(string message) => new DuckTypeCustomAttributeHasNamedArgumentsException(message, true);
    }

    /// <summary>
    /// Ducktype DuckCopy struct does not contains any field
    /// </summary>
    internal sealed class DuckTypeDuckCopyStructDoesNotContainsAnyField : DuckTypeException
    {
        private DuckTypeDuckCopyStructDoesNotContainsAnyField(string type)
            : base($"The [DuckCopy] struct '{type}' does not contains any public field. Remember that DuckCopy proxies must be declared using fields instead of properties.")
        {
        }

        private DuckTypeDuckCopyStructDoesNotContainsAnyField(string message, bool useRawMessage)
            : base(message)
        {
        }

        internal static DuckTypeDuckCopyStructDoesNotContainsAnyField Create(Type type) => new(type?.FullName ?? type?.Name ?? "NULL");

        internal static Exception CreateForAot(string message) => new DuckTypeDuckCopyStructDoesNotContainsAnyField(message, true);
    }

    /// <summary>
    /// DuckType runtime mode was configured with a different value.
    /// </summary>
    internal sealed class DuckTypeRuntimeModeConflictException : DuckTypeException
    {
        private DuckTypeRuntimeModeConflictException(DuckTypeRuntimeMode currentMode, DuckTypeRuntimeMode requestedMode)
            : base($"DuckType runtime mode is immutable after initialization. Current mode: '{currentMode}', requested mode: '{requestedMode}'.")
        {
        }

        [DebuggerHidden]
        [DoesNotReturn]
        internal static void Throw(DuckTypeRuntimeMode currentMode, DuckTypeRuntimeMode requestedMode)
        {
            throw new DuckTypeRuntimeModeConflictException(currentMode, requestedMode);
        }
    }

    /// <summary>
    /// No AOT proxy registration exists for the requested pair.
    /// </summary>
    internal sealed class DuckTypeAotMissingProxyRegistrationException : DuckTypeException
    {
        private DuckTypeAotMissingProxyRegistrationException(string message)
            : base(message)
        {
        }

        internal static DuckTypeAotMissingProxyRegistrationException Create(Type proxyDefinitionType, Type targetType, bool reverse)
            => Create(proxyDefinitionType, targetType, reverse, failedRegistrationCount: 0, firstRegistrationFailure: null);

        /// <summary>
        /// Creates the exception, naming the registrations of the registry that failed at startup (one of them may be the missing one).
        /// </summary>
        /// <param name="proxyDefinitionType">The proxy definition type.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="reverse">Whether the mapping is a reverse one.</param>
        /// <param name="failedRegistrationCount">The number of registrations that failed.</param>
        /// <param name="firstRegistrationFailure">The exception of the first one.</param>
        /// <returns>The exception.</returns>
        internal static DuckTypeAotMissingProxyRegistrationException Create(Type proxyDefinitionType, Type targetType, bool reverse, int failedRegistrationCount, string? firstRegistrationFailure)
        {
            var message = $"AOT duck typing mapping not found for proxy '{proxyDefinitionType.FullName}' and target '{targetType.FullName}' (reverse={reverse}).";
            return new(failedRegistrationCount > 0
                           ? $"{message} {failedRegistrationCount} registration(s) of the AOT duck typing registry failed at startup, because they use types this runtime can't load (the first one: {firstRegistrationFailure})."
                           : message);
        }
    }

    /// <summary>
    /// AOT proxy registration for a key already exists with a different generated proxy type.
    /// </summary>
    internal sealed class DuckTypeAotProxyRegistrationConflictException : DuckTypeException
    {
        private DuckTypeAotProxyRegistrationConflictException(Type proxyDefinitionType, Type targetType, bool reverse, Type existingProxyType, Type newProxyType)
            : base($"Conflicting AOT duck typing registration for proxy '{proxyDefinitionType.FullName}' and target '{targetType.FullName}' (reverse={reverse}). Existing generated proxy: '{existingProxyType.FullName}', new generated proxy: '{newProxyType.FullName}'.")
        {
        }

        [DebuggerHidden]
        [DoesNotReturn]
        internal static void Throw(Type proxyDefinitionType, Type targetType, bool reverse, Type existingProxyType, Type newProxyType)
        {
            throw new DuckTypeAotProxyRegistrationConflictException(proxyDefinitionType, targetType, reverse, existingProxyType, newProxyType);
        }
    }

    /// <summary>
    /// Generated AOT proxy type does not satisfy the proxy definition contract.
    /// </summary>
    internal sealed class DuckTypeAotGeneratedProxyTypeMismatchException : DuckTypeException
    {
        private DuckTypeAotGeneratedProxyTypeMismatchException(Type proxyDefinitionType, Type generatedProxyType)
            : base($"Generated AOT proxy type '{generatedProxyType.FullName}' is not assignable to proxy definition '{proxyDefinitionType.FullName}'.")
        {
        }

        [DebuggerHidden]
        [DoesNotReturn]
        internal static void Throw(Type proxyDefinitionType, Type generatedProxyType)
        {
            throw new DuckTypeAotGeneratedProxyTypeMismatchException(proxyDefinitionType, generatedProxyType);
        }
    }

    /// <summary>
    /// Multiple AOT registry assemblies attempted to register mappings in the same process.
    /// </summary>
    internal sealed class DuckTypeAotMultipleRegistryAssembliesException : DuckTypeException
    {
        private DuckTypeAotMultipleRegistryAssembliesException(string currentRegistryAssembly, string newRegistryAssembly)
            : base($"AOT duck typing supports a single generated registry assembly per process. Current registry assembly: '{currentRegistryAssembly}', attempted registration from: '{newRegistryAssembly}'.")
        {
        }

        [DebuggerHidden]
        [DoesNotReturn]
        internal static void Throw(string currentRegistryAssembly, string newRegistryAssembly)
        {
            throw new DuckTypeAotMultipleRegistryAssembliesException(currentRegistryAssembly, newRegistryAssembly);
        }
    }

    /// <summary>
    /// Generated AOT registry contract is invalid for the current Datadog.Trace runtime.
    /// </summary>
    internal sealed class DuckTypeAotRegistryContractValidationException : DuckTypeException
    {
        private DuckTypeAotRegistryContractValidationException(string detail)
            : base($"AOT registry contract validation failed. {detail}")
        {
        }

        [DebuggerHidden]
        [DoesNotReturn]
        internal static void ThrowValidation(string detail)
        {
            throw new DuckTypeAotRegistryContractValidationException(detail);
        }
    }

    /// <summary>
    /// Represents a deterministic registered AOT failure replay.
    /// </summary>
    internal sealed class DuckTypeAotRegisteredFailureException : DuckTypeException
    {
        private DuckTypeAotRegisteredFailureException(string failureTypeName, string detail)
            : base(StringUtil.IsNullOrWhiteSpace(detail)
                       ? $"AOT duck typing registered failure '{failureTypeName}' was replayed."
                       : $"AOT duck typing registered failure '{failureTypeName}' was replayed. {detail}")
        {
        }

        internal static Exception Create(string failureTypeName, string detail)
        {
            return failureTypeName switch
            {
                string name when name == typeof(DuckTypeException).FullName => DuckTypeException.Create(detail),
                string name when name == typeof(DuckTypeInvalidTypeConversionException).FullName => DuckTypeInvalidTypeConversionException.CreateForAot(detail),
                string name when name == typeof(DuckTypePropertyCantBeReadException).FullName => DuckTypePropertyCantBeReadException.CreateForAot(detail),
                string name when name == typeof(DuckTypePropertyCantBeWrittenException).FullName => DuckTypePropertyCantBeWrittenException.CreateForAot(detail),
                string name when name == typeof(DuckTypePropertyArgumentsLengthException).FullName => DuckTypePropertyArgumentsLengthException.CreateForAot(detail),
                string name when name == typeof(DuckTypeFieldIsReadonlyException).FullName => DuckTypeFieldIsReadonlyException.CreateForAot(detail),
                string name when name == typeof(DuckTypePropertyOrFieldNotFoundException).FullName => DuckTypePropertyOrFieldNotFoundException.CreateForAot(detail),
                string name when name == typeof(DuckTypeStructMembersCannotBeChangedException).FullName => DuckTypeStructMembersCannotBeChangedException.CreateForAot(detail),
                string name when name == typeof(DuckTypeTargetMethodNotFoundException).FullName => DuckTypeTargetMethodNotFoundException.CreateForAot(detail),
                string name when name == typeof(DuckTypeProxyMethodParameterIsMissingException).FullName => DuckTypeProxyMethodParameterIsMissingException.CreateForAot(detail),
                string name when name == typeof(DuckTypeProxyAndTargetMethodParameterSignatureMismatchException).FullName => DuckTypeProxyAndTargetMethodParameterSignatureMismatchException.CreateForAot(detail),
                string name when name == typeof(DuckTypeProxyAndTargetMethodReturnTypeMismatchException).FullName => DuckTypeProxyAndTargetMethodReturnTypeMismatchException.CreateForAot(detail),
                string name when name == typeof(DuckTypeProxyMethodsWithGenericParametersNotSupportedInNonPublicInstancesException).FullName => DuckTypeProxyMethodsWithGenericParametersNotSupportedInNonPublicInstancesException.CreateForAot(detail),
                string name when name == typeof(DuckTypeTargetMethodAmbiguousMatchException).FullName => DuckTypeTargetMethodAmbiguousMatchException.CreateForAot(detail),
                string name when name == typeof(DuckTypeTargetPropertyAmbiguousMatchException).FullName => DuckTypeTargetPropertyAmbiguousMatchException.CreateForAot(detail),
                string name when name == typeof(DuckTypeReverseProxyBaseIsStructException).FullName => DuckTypeReverseProxyBaseIsStructException.CreateForAot(detail),
                string name when name == typeof(DuckTypeReverseProxyImplementorIsAbstractOrInterfaceException).FullName => DuckTypeReverseProxyImplementorIsAbstractOrInterfaceException.CreateForAot(detail),
                string name when name == typeof(DuckTypeReverseProxyPropertyCannotBeAbstractException).FullName => DuckTypeReverseProxyPropertyCannotBeAbstractException.CreateForAot(detail),
                string name when name == typeof(DuckTypeIncorrectReverseMethodUsageException).FullName => DuckTypeIncorrectReverseMethodUsageException.CreateForAot(detail),
                string name when name == typeof(DuckTypeIncorrectReversePropertyUsageException).FullName => DuckTypeIncorrectReversePropertyUsageException.CreateForAot(detail),
                string name when name == typeof(DuckTypeReverseProxyMissingPropertyImplementationException).FullName => DuckTypeReverseProxyMissingPropertyImplementationException.CreateForAot(detail),
                string name when name == typeof(DuckTypeReverseProxyMissingMethodImplementationException).FullName => DuckTypeReverseProxyMissingMethodImplementationException.CreateForAot(detail),
                string name when name == typeof(DuckTypeReverseAttributeParameterNamesMismatchException).FullName => DuckTypeReverseAttributeParameterNamesMismatchException.CreateForAot(detail),
                string name when name == typeof(DuckTypeReverseProxyMustImplementGenericMethodAsGenericException).FullName => DuckTypeReverseProxyMustImplementGenericMethodAsGenericException.CreateForAot(detail),
                string name when name == typeof(DuckTypeCustomAttributeHasNamedArgumentsException).FullName => DuckTypeCustomAttributeHasNamedArgumentsException.CreateForAot(detail),
                string name when name == typeof(DuckTypeDuckCopyStructDoesNotContainsAnyField).FullName => DuckTypeDuckCopyStructDoesNotContainsAnyField.CreateForAot(detail),
                _ => new DuckTypeAotRegisteredFailureException(failureTypeName, detail)
            };
        }

        /// <summary>
        /// Creates the exception dynamic duck typing throws, with the exception it wraps (e.g. the TypeLoadException of
        /// Reflection.Emit in a DuckTypeException).
        /// </summary>
        /// <param name="failureTypeName">The full name of the exception type.</param>
        /// <param name="detail">The message of the exception.</param>
        /// <param name="innerExceptionTypeName">The full name of the type of the wrapped exception.</param>
        /// <param name="innerExceptionMessage">The message of the wrapped exception.</param>
        /// <returns>The exception.</returns>
        internal static Exception Create(string failureTypeName, string detail, string innerExceptionTypeName, string innerExceptionMessage)
        {
            Exception? innerException = innerExceptionTypeName switch
            {
                string name when name == typeof(TypeLoadException).FullName => new TypeLoadException(innerExceptionMessage),
                string name when name == typeof(AmbiguousMatchException).FullName => new AmbiguousMatchException(innerExceptionMessage),
                string name when name == typeof(InvalidOperationException).FullName => new InvalidOperationException(innerExceptionMessage),
                string name when name == typeof(ArgumentException).FullName => new ArgumentException(innerExceptionMessage),
                string name when name == typeof(NotSupportedException).FullName => new NotSupportedException(innerExceptionMessage),
                string name when name == typeof(BadImageFormatException).FullName => new BadImageFormatException(innerExceptionMessage),
                string name when name == typeof(FileLoadException).FullName => new FileLoadException(innerExceptionMessage),
                string name when name == typeof(MissingMethodException).FullName => new MissingMethodException(innerExceptionMessage),
                string name when name == typeof(MissingFieldException).FullName => new MissingFieldException(innerExceptionMessage),
                string name when name == typeof(InvalidCastException).FullName => new InvalidCastException(innerExceptionMessage),
                string name when name == typeof(MethodAccessException).FullName => new MethodAccessException(innerExceptionMessage),
                string name when name == typeof(FieldAccessException).FullName => new FieldAccessException(innerExceptionMessage),
                string name when name == typeof(TypeAccessException).FullName => new TypeAccessException(innerExceptionMessage),
                string name when name == typeof(InvalidProgramException).FullName => new InvalidProgramException(innerExceptionMessage),
                _ => null
            };

            return Create(failureTypeName, detail, innerException);
        }

        /// <summary>
        /// Creates the exception dynamic duck typing throws, with the exception it wraps (only a DuckTypeException wraps one).
        /// </summary>
        /// <param name="failureTypeName">The full name of the exception type.</param>
        /// <param name="detail">The message of the exception.</param>
        /// <param name="innerException">The wrapped exception, or null.</param>
        /// <returns>The exception.</returns>
        internal static Exception Create(string failureTypeName, string detail, Exception? innerException)
            => innerException is not null && failureTypeName == typeof(DuckTypeException).FullName
                   ? DuckTypeException.Create(detail, innerException)
                   : Create(failureTypeName, detail);

        [DebuggerHidden]
        [DoesNotReturn]
        internal static void Throw(string failureTypeName, string detail)
        {
            throw Create(failureTypeName, detail);
        }
    }
}
