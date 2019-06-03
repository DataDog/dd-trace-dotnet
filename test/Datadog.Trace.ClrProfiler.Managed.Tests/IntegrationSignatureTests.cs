using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    public class IntegrationSignatureTests
    {
        [SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1009:ClosingParenthesisMustBeSpacedCorrectly", Justification = "Reviewed.")]
        public static IEnumerable<object[]> GetWrapperMethods()
        {
            var integrationsAssembly = typeof(Instrumentation).Assembly;

            // find all methods in Datadog.Trace.ClrProfiler.Managed.dll with [InterceptMethod]
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
            // parameter for the original method call's opcode
            ParameterInfo lastParameter = wrapperMethod.GetParameters().Last();
            Assert.Equal(typeof(int), lastParameter.ParameterType);
            Assert.Equal("opCode", lastParameter.Name);
        }
    }
}
