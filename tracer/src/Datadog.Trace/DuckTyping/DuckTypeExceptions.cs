// <copyright file="DuckTypeExceptions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
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

        internal static DuckTypeInvalidTypeConversionException Create(Type actualType, Type expectedType) => new(actualType, expectedType);
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

        internal static DuckTypePropertyCantBeReadException Create(PropertyInfo property) => new(property);
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

        internal static DuckTypePropertyCantBeWrittenException Create(PropertyInfo property) => new(property);
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

        internal static DuckTypePropertyArgumentsLengthException Create(PropertyInfo property) => new(property);
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

        internal static DuckTypeFieldIsReadonlyException Create(FieldInfo field) => new(field);
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

        internal static DuckTypePropertyOrFieldNotFoundException Create(string name, string duckAttributeName, Type type)
            => new(name, duckAttributeName, type?.FullName ?? type?.Name ?? "NULL");
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

        internal static DuckTypeStructMembersCannotBeChangedException Create(Type type) => new(type);
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

        internal static DuckTypeTargetMethodNotFoundException Create(MethodInfo method) => new(method);
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

        internal static DuckTypeProxyMethodParameterIsMissingException Create(MethodInfo proxyMethod, ParameterInfo targetParameterInfo)
            => new(proxyMethod, targetParameterInfo);
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

        internal static DuckTypeProxyAndTargetMethodParameterSignatureMismatchException Create(MethodInfo proxyMethod, MethodInfo targetMethod) => new(proxyMethod, targetMethod);
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

        internal static DuckTypeProxyAndTargetMethodReturnTypeMismatchException Create(MethodInfo proxyMethod, MethodInfo targetMethod) => new(proxyMethod, targetMethod);
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

        internal static DuckTypeProxyMethodsWithGenericParametersNotSupportedInNonPublicInstancesException Create(MethodInfo proxyMethod) => new(proxyMethod);
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

        internal static DuckTypeTargetMethodAmbiguousMatchException Create(MethodInfo proxyMethod, MethodInfo targetMethod, MethodInfo targetMethod2) => new(proxyMethod, targetMethod, targetMethod2);
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

        internal static DuckTypeTargetPropertyAmbiguousMatchException Create(Type targetType, string propertyName) => new(targetType, propertyName);
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

        internal static DuckTypeReverseProxyBaseIsStructException Create(Type type) => new(type);
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

        internal static DuckTypeReverseProxyImplementorIsAbstractOrInterfaceException Create(Type type) => new(type);
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

        internal static DuckTypeReverseProxyPropertyCannotBeAbstractException Create(PropertyInfo property) => new(property);
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

        internal static DuckTypeIncorrectReverseMethodUsageException Create(MethodInfo method) => new(method);
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

        internal static DuckTypeIncorrectReversePropertyUsageException Create(PropertyInfo property) => new(property);
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

        internal static DuckTypeReverseProxyMissingPropertyImplementationException Create(IEnumerable<PropertyInfo> properties) => new(properties);
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

        internal static DuckTypeReverseProxyMissingMethodImplementationException Create(IEnumerable<MethodInfo> methods) => new(methods);
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

        internal static DuckTypeReverseAttributeParameterNamesMismatchException Create(MethodInfo method) => new(method);
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

        internal static DuckTypeReverseProxyMustImplementGenericMethodAsGenericException Create(MethodInfo implementationMethod, MethodInfo targetMethod) => new(implementationMethod, targetMethod);
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

        internal static DuckTypeCustomAttributeHasNamedArgumentsException Create(Type type, CustomAttributeData attributeData) => new(attributeData.AttributeType?.FullName ?? "Null", type?.FullName ?? type?.Name ?? "NULL");
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

        internal static DuckTypeDuckCopyStructDoesNotContainsAnyField Create(Type type) => new(type?.FullName ?? type?.Name ?? "NULL");
    }
}
