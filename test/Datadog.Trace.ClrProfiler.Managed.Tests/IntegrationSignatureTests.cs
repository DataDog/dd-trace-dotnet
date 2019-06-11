using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datadog.Trace.ClrProfiler.Integrations;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    public class IntegrationSignatureTests
    {
        private static readonly List<MethodInfo> StaticInstrumentations = new List<MethodInfo>()
        {
            typeof(AspNetCoreMvc2Integration).GetMethod(nameof(AspNetCoreMvc2Integration.BeforeAction)),
            typeof(AspNetCoreMvc2Integration).GetMethod(nameof(AspNetCoreMvc2Integration.AfterAction)),
            typeof(AspNetCoreMvc2Integration).GetMethod(nameof(AspNetCoreMvc2Integration.Rethrow)),
        };

        public static IEnumerable<object[]> GetWrapperMethodWithInterceptionAttributes()
        {
            var integrationsAssembly = typeof(Instrumentation).Assembly;

            foreach (var wrapperMethod in integrationsAssembly.GetTypes().SelectMany(t => t.GetRuntimeMethods()))
            {
                foreach (var interceptionAttribute in wrapperMethod.GetCustomAttributes<InterceptMethodAttribute>(inherit: false))
                {
                    yield return new object[] { wrapperMethod, interceptionAttribute };
                }
            }
        }

        public static IEnumerable<object[]> GetWrapperMethods()
        {
            return GetWrapperMethodWithInterceptionAttributes().Select(i => new[] { i[0] }).Distinct();
        }

        [Theory]
        [MemberData(nameof(GetWrapperMethods))]
        public void WrapperMethodHasOpCodeArgument(MethodInfo wrapperMethod)
        {
            // all wrapper methods should have an additional Int32
            // parameter for the original method call's opcode
            ParameterInfo lastParameter = wrapperMethod.GetParameters().Last();
            Assert.Equal(typeof(int), lastParameter.ParameterType);
            Assert.Equal("opCode", lastParameter.Name);
        }

        [Theory]
        [MemberData(nameof(GetWrapperMethodWithInterceptionAttributes))]
        public void AllMethodsHaveProperlyFormedTargetSignatureTypes(MethodInfo wrapperMethod, InterceptMethodAttribute attribute)
        {
            Assert.True(
                attribute.TargetSignatureTypes != null,
                $"{wrapperMethod.DeclaringType.Name}.{wrapperMethod.Name}: {nameof(attribute.TargetSignatureTypes)} definition missing.");

            // Return type and opcode cancel out for count
            var expectedParameterCount = wrapperMethod.GetParameters().Length;

            if (!StaticInstrumentations.Contains(wrapperMethod))
            {
                // Subtract for the instance (this) parameter
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
