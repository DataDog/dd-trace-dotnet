// <copyright file="DuckTypeAotExceptions.cs" company="Datadog">
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

namespace Datadog.Trace.DuckTyping.Aot
{
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
