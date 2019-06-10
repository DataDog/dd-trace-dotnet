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
            typeof(AspNetCoreMvc2Integration).GetMethod(nameof(AspNetCoreMvc2Integration.Rethrow)),
        };

        public static IEnumerable<object[]> GetWrapperMethods()
        {
            var integrationsAssembly = typeof(Instrumentation).Assembly;

            var integrations = from wrapperType in integrationsAssembly.GetTypes()
                               from wrapperMethod in wrapperType.GetRuntimeMethods()
                               let attributes = wrapperMethod.GetCustomAttributes<InterceptMethodAttribute>(inherit: false)
                               where attributes.Any()
                               select new object[] { wrapperMethod };

            return integrations;
        }

        [Theory]
        [MemberData(nameof(GetWrapperMethods))]
        public void WrapperMethodHasOpCodeArgument(MethodInfo wrapperMethod)
        {
            // all wrapper methods should have an additional Int32
            // parameter for the original method call's op-code
            ParameterInfo lastParameter = wrapperMethod.GetParameters().Last();
            Assert.Equal(typeof(int), lastParameter.ParameterType);
            Assert.Equal("opCode", lastParameter.Name);
        }

        [Theory]
        [MemberData(nameof(GetWrapperMethods))]
        public void AllMethodsHaveProperlyFormedTargetSignatureTypes(MethodInfo wrapperMethod)
        {
            var attribute = wrapperMethod.GetCustomAttributes<InterceptMethodAttribute>(inherit: false).Single();

            Assert.True(
                attribute.TargetSignatureTypes != null,
                $"{wrapperMethod.DeclaringType.Name}.{wrapperMethod.Name}: {nameof(attribute.TargetSignatureTypes)} definition missing.");

            var expectedParameterCount = wrapperMethod.GetParameters().Length;

            // Return type
            expectedParameterCount++;

            if (StaticInstrumentations.Contains(wrapperMethod))
            {
                // no instance parameter
                expectedParameterCount--;
            }

            var typeSigLength = attribute.TargetSignatureTypes.Length;
            Assert.True(
                expectedParameterCount == typeSigLength,
                $"{wrapperMethod.DeclaringType.Name}.{wrapperMethod.Name}: {nameof(attribute.TargetSignatureTypes)} has {typeSigLength} items, expected {expectedParameterCount}.");
        }
    }
}
