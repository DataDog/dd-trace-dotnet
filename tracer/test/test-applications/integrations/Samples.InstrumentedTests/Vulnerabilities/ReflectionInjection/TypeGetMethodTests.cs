using System;
using System.Reflection;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.ReflectionInjection;

public class TypeGetMethodTests : InstrumentationTestsBase
{
    protected string taintedMethod;
    protected string notTaintedMethod;

    public TypeGetMethodTests()
    {
        taintedMethod = nameof(TypeTestsType.TaintedSimpleMethod);
        notTaintedMethod = nameof(TypeTestsType.NotTaintedSimpleMethod);
        AddTainted(taintedMethod);
    }
    
#if !NETFRAMEWORK
    // for GetMethod(System.String,System.Int32,System.Reflection.BindingFlags,System.Reflection.Binder,System.Reflection.CallingConventions,System.Type[],System.Reflection.ParameterModifier[])
    [Fact]
    public void GivenAType_GetMethodTainted_Method1_VulnerabilityIsReported()
    {
        typeof(TypeTestsType).GetMethod(taintedMethod, 0, BindingFlags.Default, null, CallingConventions.Any, [], null);
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAType_GetMethodNotTainted_Method1_NotVulnerable()
    {
        typeof(TypeTestsType).GetMethod(notTaintedMethod, 0, BindingFlags.Default, null, CallingConventions.Any, [], null);
        AssertNotVulnerable();
    }
#endif

    // for GetMethod(System.String,System.Reflection.BindingFlags,System.Reflection.Binder,System.Reflection.CallingConventions,System.Type[],System.Reflection.ParameterModifier[])
    [Fact]
    public void GivenAType_GetMethodTainted_Method2_VulnerabilityIsReported()
    {
        typeof(TypeTestsType).GetMethod(taintedMethod, BindingFlags.Default, null, CallingConventions.Any, [], null);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAType_GetMethodNotTainted_Method2_NotVulnerable()
    {
        typeof(TypeTestsType).GetMethod(notTaintedMethod, BindingFlags.Default, null, CallingConventions.Any, [], null);
        AssertNotVulnerable();
    }
    
#if !NETFRAMEWORK
    // for GetMethod(System.String,System.Int32,System.Reflection.BindingFlags,System.Reflection.Binder,System.Type[],System.Reflection.ParameterModifier[])
    [Fact]
    public void GivenAType_GetMethodTainted_Method3_VulnerabilityIsReported()
    {
        typeof(TypeTestsType).GetMethod(taintedMethod, 0, BindingFlags.Default, null, [], null);
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAType_GetMethodNotTainted_Method3_NotVulnerable()
    {
        typeof(TypeTestsType).GetMethod(notTaintedMethod, 0, BindingFlags.Default, null, [], null);
        AssertNotVulnerable();
    }
#endif

    // for GetMethod(System.String,System.Reflection.BindingFlags,System.Reflection.Binder,System.Type[],System.Reflection.ParameterModifier[])
    [Fact]
    public void GivenAType_GetMethodTainted_Method4_VulnerabilityIsReported()
    {
        typeof(TypeTestsType).GetMethod(taintedMethod, BindingFlags.Default, null, [], null);
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAType_GetMethodNotTainted_Method4_NotVulnerable()
    {
        typeof(TypeTestsType).GetMethod(notTaintedMethod, BindingFlags.Default, null, [], null);
        AssertNotVulnerable();
    }

#if !NETFRAMEWORK
    // for GetMethod(System.String,System.Int32,System.Type[],System.Reflection.ParameterModifier[])
    [Fact]
    public void GivenAType_GetMethodTainted_Method5_VulnerabilityIsReported()
    {
        typeof(TypeTestsType).GetMethod(taintedMethod, 0, [], null);
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAType_GetMethodNotTainted_Method5_NotVulnerable()
    {
        typeof(TypeTestsType).GetMethod(notTaintedMethod, 0, [], null);
        AssertNotVulnerable();
    }
#endif

    // for GetMethod(System.String)
    [Fact]
    public void GivenAType_GetMethodTainted_Method6_VulnerabilityIsReported()
    {
        typeof(TypeTestsType).GetMethod(taintedMethod);
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAType_GetMethodNotTainted_Method6_NotVulnerable()
    {
        typeof(TypeTestsType).GetMethod(notTaintedMethod);
        AssertNotVulnerable();
    }
    
#if NET6_0_OR_GREATER
    // for GetMethod(System.String,System.Reflection.BindingFlags,System.Type[])
    [Fact]
    public void GivenAType_GetMethodTainted_Method7_VulnerabilityIsReported()
    {
        typeof(TypeTestsType).GetMethod(taintedMethod, BindingFlags.Default, []);
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAType_GetMethodNotTainted_Method7_NotVulnerable()
    {
        typeof(TypeTestsType).GetMethod(notTaintedMethod, BindingFlags.Default, []);
        AssertNotVulnerable();
    }
#endif
    
#if !NETFRAMEWORK
    // for GetMethod(System.String,System.Int32,System.Type[])
    [Fact]
    public void GivenAType_GetMethodTainted_Method8_VulnerabilityIsReported()
    {
        typeof(TypeTestsType).GetMethod(taintedMethod, 0, []);
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAType_GetMethodNotTainted_Method8_NotVulnerable()
    {
        typeof(TypeTestsType).GetMethod(notTaintedMethod, 0, []);
        AssertNotVulnerable();
    }
#endif

    // for GetMethod(System.String,System.Type[])
    [Fact]
    public void GivenAType_GetMethodTainted_Method9_VulnerabilityIsReported()
    {
        typeof(TypeTestsType).GetMethod(taintedMethod, []);
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAType_GetMethodNotTainted_Method9_NotVulnerable()
    {
        typeof(TypeTestsType).GetMethod(notTaintedMethod, []);
        AssertNotVulnerable();
    }
    
    // for GetMethod(System.String,System.Reflection.BindingFlags)
    [Fact]
    public void GivenAType_GetMethodTainted_Method10_VulnerabilityIsReported()
    {
        typeof(TypeTestsType).GetMethod(taintedMethod, BindingFlags.Default);
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAType_GetMethodNotTainted_Method10_NotVulnerable()
    {
        typeof(TypeTestsType).GetMethod(notTaintedMethod, BindingFlags.Default);
        AssertNotVulnerable();
    }
    
    // for GetMethod(System.String,System.Type[],System.Reflection.ParameterModifier[])
    [Fact]
    public void GivenAType_GetMethodTainted_Method11_VulnerabilityIsReported()
    {
        typeof(TypeTestsType).GetMethod(taintedMethod, [], null);
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAType_GetMethodNotTainted_Method11_NotVulnerable()
    {
        typeof(TypeTestsType).GetMethod(notTaintedMethod, [], null);
        AssertNotVulnerable();
    }
}
