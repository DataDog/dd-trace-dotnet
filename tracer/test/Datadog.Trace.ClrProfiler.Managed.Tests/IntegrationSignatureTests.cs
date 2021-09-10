// <copyright file="IntegrationSignatureTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    public class IntegrationSignatureTests
    {
        // This is a list of instrumented methods that are static, i.e., the target method is static.
        private static readonly List<MethodInfo> StaticInstrumentations = new List<MethodInfo>()
        {
            // This list is currently empty
        };

        public static IEnumerable<object[]> GetCallSiteMethodsWithInterceptionAttribute()
        {
            var integrationsAssembly = typeof(Instrumentation).Assembly;

            foreach (var wrapperMethod in integrationsAssembly.GetTypes().SelectMany(t => t.GetRuntimeMethods()))
            {
                foreach (var interceptionAttribute in wrapperMethod.GetCustomAttributes<InterceptMethodAttribute>(inherit: false))
                {
                    if (interceptionAttribute.MethodReplacementAction == MethodReplacementActionType.ReplaceTargetMethod)
                    {
                        yield return new object[] { wrapperMethod, interceptionAttribute };
                    }
                }
            }
        }

        public static IEnumerable<object[]> GetCallSiteMethods()
        {
            return GetCallSiteMethodsWithInterceptionAttribute().Select(i => new[] { i[0] }).Distinct();
        }

        [Theory]
        [MemberData(nameof(GetCallSiteMethods))]
        public void CallSiteMethodHasOpCodeArgument(MethodInfo wrapperMethod)
        {
            // all wrapper methods should have an additional Int32
            // parameter for the original method call's opcode
            var parameters = wrapperMethod.GetParameters();
            var param = parameters[parameters.Length - 3];
            Assert.Equal(typeof(int), param.ParameterType);
            Assert.Equal("opCode", param.Name);
        }

        [Theory]
        [MemberData(nameof(GetCallSiteMethods))]
        public void CallSiteMethodHasMdTokenArgument(MethodInfo wrapperMethod)
        {
            // all wrapper methods should have an additional Int32
            // parameter for the original method call's mdToken
            var parameters = wrapperMethod.GetParameters();
            var param = parameters[parameters.Length - 2];
            Assert.Equal(typeof(int), param.ParameterType);
            Assert.Equal("mdToken", param.Name);
        }

        [Theory]
        [MemberData(nameof(GetCallSiteMethods))]
        public void CallSiteMethodHasModuleVersionPtrArgument(MethodInfo wrapperMethod)
        {
            // all wrapper methods should have an additional Int64
            // parameter for the address of calling module's moduleVersionId
            var parameters = wrapperMethod.GetParameters();
            var param = parameters[parameters.Length - 1];
            Assert.Equal(typeof(long), param.ParameterType);
            Assert.Equal("moduleVersionPtr", param.Name);
        }

        [Theory]
        [MemberData(nameof(GetCallSiteMethodsWithInterceptionAttribute))]
        internal void AllMethodsHaveProperlyFormedTargetSignatureTypes(MethodInfo wrapperMethod, InterceptMethodAttribute attribute)
        {
            Assert.True(
                attribute.TargetSignatureTypes != null,
                $"{wrapperMethod.DeclaringType.Name}.{wrapperMethod.Name}: {nameof(attribute.TargetSignatureTypes)} definition missing.");

            // add 1 for return type, subtract 3 for extra parameters (opcode, mdToken, moduleVersionPtr)
            // 1 - 3 = -2
            var expectedParameterCount = wrapperMethod.GetParameters().Length - 2;

            if (!StaticInstrumentations.Contains(wrapperMethod))
            {
                // Subtract the instance (this) parameter
                expectedParameterCount--;
            }

            var typeSigLength = attribute.TargetSignatureTypes.Length;
            Assert.True(
                expectedParameterCount == typeSigLength,
                $"{wrapperMethod.DeclaringType.Name}.{wrapperMethod.Name}: {nameof(attribute.TargetSignatureTypes)} has {typeSigLength} items, expected {expectedParameterCount}.");

            Assert.False(
                attribute.TargetSignatureTypes.Any(string.IsNullOrWhiteSpace),
                $"{wrapperMethod.DeclaringType.Name}.{wrapperMethod.Name}: {nameof(attribute.TargetSignatureTypes)} has null or empty arguments.");
        }
    }
}
