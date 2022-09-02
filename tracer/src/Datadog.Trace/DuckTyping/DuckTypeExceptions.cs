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

        [DebuggerHidden]
        [DoesNotReturn]
        internal static void Throw(string message)
        {
            throw new DuckTypeException(message);
        }

        [DebuggerHidden]
        [DoesNotReturn]
        internal static void Throw(string message, Exception innerException)
        {
            throw new DuckTypeException(message, innerException);
        }
    }

    /// <summary>
    /// DuckType proxy type definition is null
    /// </summary>
    internal class DuckTypeProxyTypeDefinitionIsNull : DuckTypeException
    {
        private DuckTypeProxyTypeDefinitionIsNull()
            : base($"The proxy type definition is null.")
        {
        }

        [DebuggerHidden]
        [DoesNotReturn]
        internal static void Throw()
        {
            throw new DuckTypeProxyTypeDefinitionIsNull();
        }
    }

    /// <summary>
    /// DuckType target object instance is null
    /// </summary>
    internal class DuckTypeTargetObjectInstanceIsNull : DuckTypeException
    {
        private DuckTypeTargetObjectInstanceIsNull()
            : base($"The target object instance is null.")
        {
        }

        [DebuggerHidden]
        [DoesNotReturn]
        internal static void Throw()
        {
            throw new DuckTypeTargetObjectInstanceIsNull();
        }
    }

    /// <summary>
    /// DuckType invalid type conversion exception
    /// </summary>
    internal class DuckTypeInvalidTypeConversionException : DuckTypeException
    {
        private DuckTypeInvalidTypeConversionException(Type actualType, Type expectedType)
            : base($"Invalid type conversion from {actualType.FullName} to {expectedType.FullName}")
        {
        }

        [DebuggerHidden]
        [DoesNotReturn]
        internal static void Throw(Type actualType, Type expectedType)
        {
            throw new DuckTypeInvalidTypeConversionException(actualType, expectedType);
        }
    }

    /// <summary>
    /// DuckType property can't be read
    /// </summary>
    internal class DuckTypePropertyCantBeReadException : DuckTypeException
    {
        private DuckTypePropertyCantBeReadException(PropertyInfo property)
            : base($"The property '{property.Name}' can't be read, you should remove the getter from the proxy definition base type class or interface.")
        {
        }

        [DebuggerHidden]
        [DoesNotReturn]
        internal static void Throw(PropertyInfo property)
        {
            throw new DuckTypePropertyCantBeReadException(property);
        }
    }

    /// <summary>
    /// DuckType property can't be written
    /// </summary>
    internal class DuckTypePropertyCantBeWrittenException : DuckTypeException
    {
        private DuckTypePropertyCantBeWrittenException(PropertyInfo property)
            : base($"The property '{property.Name}' can't be written, you should remove the setter from the proxy definition base type class or interface.")
        {
        }

        [DebuggerHidden]
        [DoesNotReturn]
        internal static void Throw(PropertyInfo property)
        {
            throw new DuckTypePropertyCantBeWrittenException(property);
        }
    }

    /// <summary>
    /// DuckType property argument doesn't have the same argument length
    /// </summary>
    internal class DuckTypePropertyArgumentsLengthException : DuckTypeException
    {
        private DuckTypePropertyArgumentsLengthException(PropertyInfo property)
            : base($"The property '{property.Name}' doesn't have the same number of arguments as the original property.")
        {
        }

        [DebuggerHidden]
        [DoesNotReturn]
        internal static void Throw(PropertyInfo property)
        {
            throw new DuckTypePropertyArgumentsLengthException(property);
        }
    }

    /// <summary>
    /// DuckType field is readonly
    /// </summary>
    internal class DuckTypeFieldIsReadonlyException : DuckTypeException
    {
        private DuckTypeFieldIsReadonlyException(FieldInfo field)
            : base($"The field '{field.Name}' is marked as readonly, you should remove the setter from the base type class or interface.")
        {
        }

        [DebuggerHidden]
        [DoesNotReturn]
        internal static void Throw(FieldInfo field)
        {
            throw new DuckTypeFieldIsReadonlyException(field);
        }
    }

    /// <summary>
    /// DuckType property or field not found
    /// </summary>
    internal class DuckTypePropertyOrFieldNotFoundException : DuckTypeException
    {
        private DuckTypePropertyOrFieldNotFoundException(string name, string duckAttributeName, string type)
            : base($"The property or field '{duckAttributeName}' for the proxy property '{name}' was not found in the instance of type '{type}'.")
        {
        }

        [DebuggerHidden]
        [DoesNotReturn]
        internal static void Throw(string name, string duckAttributeName, Type type)
        {
            throw new DuckTypePropertyOrFieldNotFoundException(name, duckAttributeName, type?.FullName ?? type?.Name ?? "NULL");
        }
    }

    /// <summary>
    /// DuckType struct members cannot be changed exception
    /// </summary>
    internal class DuckTypeStructMembersCannotBeChangedException : DuckTypeException
    {
        private DuckTypeStructMembersCannotBeChangedException(Type type)
            : base($"Modifying struct members is not supported. [{type.FullName}]")
        {
        }

        [DebuggerHidden]
        [DoesNotReturn]
        internal static void Throw(Type type)
        {
            throw new DuckTypeStructMembersCannotBeChangedException(type);
        }
    }

    /// <summary>
    /// DuckType target method can not be found exception
    /// </summary>
    internal class DuckTypeTargetMethodNotFoundException : DuckTypeException
    {
        private DuckTypeTargetMethodNotFoundException(MethodInfo method)
            : base($"The target method for the proxy method '{method}' was not found.")
        {
        }

        [DebuggerHidden]
        [DoesNotReturn]
        internal static void Throw(MethodInfo method)
        {
            throw new DuckTypeTargetMethodNotFoundException(method);
        }
    }

    /// <summary>
    /// DuckType proxy method parameter is missing exception
    /// </summary>
    internal class DuckTypeProxyMethodParameterIsMissingException : DuckTypeException
    {
        private DuckTypeProxyMethodParameterIsMissingException(MethodInfo proxyMethod, ParameterInfo targetParameterInfo)
            : base($"The proxy method '{proxyMethod.Name}' is missing parameter '{targetParameterInfo.Name}' declared in the target method.")
        {
        }

        [DebuggerHidden]
        [DoesNotReturn]
        internal static void Throw(MethodInfo proxyMethod, ParameterInfo targetParameterInfo)
        {
            throw new DuckTypeProxyMethodParameterIsMissingException(proxyMethod, targetParameterInfo);
        }
    }

    /// <summary>
    /// DuckType parameter signature mismatch between proxy and target method
    /// </summary>
    internal class DuckTypeProxyAndTargetMethodParameterSignatureMismatchException : DuckTypeException
    {
        private DuckTypeProxyAndTargetMethodParameterSignatureMismatchException(MethodInfo proxyMethod, MethodInfo targetMethod)
            : base($"Parameter signature mismatch between proxy '{proxyMethod}' and target method '{targetMethod}'")
        {
        }

        [DebuggerHidden]
        [DoesNotReturn]
        internal static void Throw(MethodInfo proxyMethod, MethodInfo targetMethod)
        {
            throw new DuckTypeProxyAndTargetMethodParameterSignatureMismatchException(proxyMethod, targetMethod);
        }
    }

    /// <summary>
    /// DuckType parameter signature mismatch between proxy and target method
    /// </summary>
    internal class DuckTypeProxyAndTargetMethodReturnTypeMismatchException : DuckTypeException
    {
        private DuckTypeProxyAndTargetMethodReturnTypeMismatchException(MethodInfo proxyMethod, MethodInfo targetMethod)
            : base($"Return type mismatch between proxy '{proxyMethod}' and target method '{targetMethod}'.")
        {
        }

        [DebuggerHidden]
        [DoesNotReturn]
        internal static void Throw(MethodInfo proxyMethod, MethodInfo targetMethod)
        {
            throw new DuckTypeProxyAndTargetMethodReturnTypeMismatchException(proxyMethod, targetMethod);
        }
    }

    /// <summary>
    /// DuckType proxy methods with generic parameters are not supported in non public instances exception
    /// </summary>
    internal class DuckTypeProxyMethodsWithGenericParametersNotSupportedInNonPublicInstancesException : DuckTypeException
    {
        private DuckTypeProxyMethodsWithGenericParametersNotSupportedInNonPublicInstancesException(MethodInfo proxyMethod)
            : base($"The proxy method with generic parameters '{proxyMethod}' are not supported on non public instances")
        {
        }

        [DebuggerHidden]
        [DoesNotReturn]
        internal static void Throw(MethodInfo proxyMethod)
        {
            throw new DuckTypeProxyMethodsWithGenericParametersNotSupportedInNonPublicInstancesException(proxyMethod);
        }
    }

    /// <summary>
    /// DuckType proxy method has an ambiguous match in the target type exception
    /// </summary>
    internal class DuckTypeTargetMethodAmbiguousMatchException : DuckTypeException
    {
        private DuckTypeTargetMethodAmbiguousMatchException(MethodInfo proxyMethod, MethodInfo targetMethod, MethodInfo targetMethod2)
            : base($"The proxy method '{proxyMethod}' matches at least two methods in the target type. Method1 = '{targetMethod}' and Method2 = '{targetMethod2}'")
        {
        }

        [DebuggerHidden]
        [DoesNotReturn]
        internal static void Throw(MethodInfo proxyMethod, MethodInfo targetMethod, MethodInfo targetMethod2)
        {
            throw new DuckTypeTargetMethodAmbiguousMatchException(proxyMethod, targetMethod, targetMethod2);
        }
    }

    /// <summary>
    /// DuckType reverse proxy type to derive from is a struct exception
    /// </summary>
    internal class DuckTypeReverseProxyBaseIsStructException : DuckTypeException
    {
        private DuckTypeReverseProxyBaseIsStructException(Type type)
            : base($"Cannot derive from struct type '{type.FullName}' for reverse proxy")
        {
        }

        [DebuggerHidden]
        [DoesNotReturn]
        internal static void Throw(Type type)
        {
            throw new DuckTypeReverseProxyBaseIsStructException(type);
        }
    }

    /// <summary>
    /// DuckType proxy method is abstract
    /// </summary>
    internal class DuckTypeReverseProxyImplementorIsAbstractOrInterfaceException : DuckTypeException
    {
        private DuckTypeReverseProxyImplementorIsAbstractOrInterfaceException(Type type)
            : base($"The implementation type '{type.FullName}' must not be an interface or abstract type for reverse proxy")
        {
        }

        [DebuggerHidden]
        [DoesNotReturn]
        internal static void Throw(Type type)
        {
            throw new DuckTypeReverseProxyImplementorIsAbstractOrInterfaceException(type);
        }
    }

    /// <summary>
    /// DuckType property can't be read
    /// </summary>
    internal class DuckTypeReverseProxyPropertyCannotBeAbstractException : DuckTypeException
    {
        private DuckTypeReverseProxyPropertyCannotBeAbstractException(PropertyInfo property)
            : base($"The property '{property.Name}' cannot be abstract for reverse proxy")
        {
        }

        [DebuggerHidden]
        [DoesNotReturn]
        internal static void Throw(PropertyInfo property)
        {
            throw new DuckTypeReverseProxyPropertyCannotBeAbstractException(property);
        }
    }

    /// <summary>
    /// DuckType method was [DuckReverseMethod] in non-reverse proxy
    /// </summary>
    internal class DuckTypeIncorrectReverseMethodUsageException : DuckTypeException
    {
        private DuckTypeIncorrectReverseMethodUsageException(MethodInfo method)
            : base($"The method '{method.Name}' was marked as a [DuckReverseMethod] but not doing reverse duck typing.")
        {
        }

        [DebuggerHidden]
        [DoesNotReturn]
        internal static void Throw(MethodInfo method)
        {
            throw new DuckTypeIncorrectReverseMethodUsageException(method);
        }
    }

    /// <summary>
    /// DuckType property was [DuckReverseMethod] in non-reverse proxy
    /// </summary>
    internal class DuckTypeIncorrectReversePropertyUsageException : DuckTypeException
    {
        private DuckTypeIncorrectReversePropertyUsageException(PropertyInfo property)
            : base($"The property '{property.Name}' was marked as a [DuckReverseMethod] but not doing reverse duck typing.")
        {
        }

        [DebuggerHidden]
        [DoesNotReturn]
        internal static void Throw(PropertyInfo property)
        {
            throw new DuckTypeIncorrectReversePropertyUsageException(property);
        }
    }

    /// <summary>
    /// DuckType proxy was missing an implementation
    /// </summary>
    internal class DuckTypeReverseProxyMissingPropertyImplementationException : DuckTypeException
    {
        private DuckTypeReverseProxyMissingPropertyImplementationException(IEnumerable<PropertyInfo> properties)
            : base($"The duck reverse proxy was missing implementations for properties: {string.Join(", ", properties.Select(x => x.Name))}")
        {
        }

        [DebuggerHidden]
        [DoesNotReturn]
        internal static void Throw(IEnumerable<PropertyInfo> properties)
        {
            throw new DuckTypeReverseProxyMissingPropertyImplementationException(properties);
        }
    }

    /// <summary>
    /// DuckType proxy was missing an implementation
    /// </summary>
    internal class DuckTypeReverseProxyMissingMethodImplementationException : DuckTypeException
    {
        private DuckTypeReverseProxyMissingMethodImplementationException(IEnumerable<MethodInfo> methods)
            : base($"The duck reverse proxy was missing implementations for methods: {string.Join(", ", methods.Select(x => x.Name))}")
        {
        }

        [DebuggerHidden]
        [DoesNotReturn]
        internal static void Throw(IEnumerable<MethodInfo> methods)
        {
            throw new DuckTypeReverseProxyMissingMethodImplementationException(methods);
        }
    }

    /// <summary>
    /// DuckType proxy tried to implement a generic method in a non-generic way
    /// </summary>
    internal class DuckTypeReverseAttributeParameterNamesMismatchException : DuckTypeException
    {
        private DuckTypeReverseAttributeParameterNamesMismatchException(MethodInfo method)
            : base($"The reverse duck attribute parameter names for method '{method.Name}' did not match the method's parameters ")
        {
        }

        [DebuggerHidden]
        [DoesNotReturn]
        internal static void Throw(MethodInfo method)
        {
            throw new DuckTypeReverseAttributeParameterNamesMismatchException(method);
        }
    }

    /// <summary>
    /// DuckType proxy tried to implement a generic method in a non-generic way
    /// </summary>
    internal class DuckTypeReverseProxyMustImplementGenericMethodAsGenericException : DuckTypeException
    {
        private DuckTypeReverseProxyMustImplementGenericMethodAsGenericException(MethodInfo implementationMethod, MethodInfo targetMethod)
            : base($"The duck reverse proxy implementation '{implementationMethod.Name}' for generic target method '{targetMethod.Name}' " +
                   $"must have same number of generic parameters - had {implementationMethod.GetGenericArguments().Length}, expected {targetMethod.GetGenericArguments().Length}")
        {
        }

        [DebuggerHidden]
        [DoesNotReturn]
        internal static void Throw(MethodInfo implementationMethod, MethodInfo targetMethod)
        {
            throw new DuckTypeReverseProxyMustImplementGenericMethodAsGenericException(implementationMethod, targetMethod);
        }
    }
}
