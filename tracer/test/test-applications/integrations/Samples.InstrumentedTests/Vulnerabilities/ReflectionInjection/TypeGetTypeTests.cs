using System;
using System.Reflection;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.ReflectionInjection;

public class TypeTestsType
{
    public string TaintedSimpleMethod()
        => "TaintedSimpleMethod";
    
    public string NotTaintedSimpleMethod()
        => "NotTaintedSimpleMethod";
}

public class TypeGetTypeTests : InstrumentationTestsBase
{
    protected string notTaintedType = "NotTaintedType";
    protected string taintedType = "TaintedType";
    protected string taintedMethod = nameof(TypeTestsType.TaintedSimpleMethod);
    protected string taintedArg1 = "TaintedArg1";
    protected string taintedArg2 = "TaintedArg2";

    public TypeGetTypeTests()
    {
        AddTainted(taintedType);
        AddTainted(taintedMethod);
        AddTainted(taintedArg1);
        AddTainted(taintedArg2);
    }

    // Tests for all variant of GetType

    [Fact]
    public void GivenAType_WhenCallingGetTypeTaintedType_VulnerabilityIsReported()
    {
        Type.GetType(taintedType);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAType_WhenCallingGetTypeNotTaintedType_NotVulnerable()
    {
        Type.GetType(notTaintedType);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAType_WhenCallingGetTypeTaintedTypeWithBoolean_VulnerabilityIsReported()
    {
        Type.GetType(taintedType, false);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAType_WhenCallingGetTypeNotTaintedTypeWithBoolean_NotVulnerable()
    {
        Type.GetType(notTaintedType, false);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAType_WhenCallingGetTypeTaintedTypeWithBooleanAndBoolean_VulnerabilityIsReported()
    {
        Type.GetType(taintedType, false, false);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAType_WhenCallingGetTypeNotTaintedTypeWithBooleanAndBoolean_NotVulnerable()
    {
        Type.GetType(notTaintedType, false, false);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAType_WhenCallingGetTypeTaintedTypeWithFunc_VulnerabilityIsReported()
    {
        Type.GetType(taintedType, null, null);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAType_WhenCallingGetTypeNotTaintedTypeWithFunc_NotVulnerable()
    {
        Type.GetType(notTaintedType, null, null);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAType_WhenCallingGetTypeTaintedTypeWithFuncAndBoolean_VulnerabilityIsReported()
    {
        Type.GetType(taintedType, null, null, false);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAType_WhenCallingGetTypeNotTaintedTypeWithFuncAndBoolean_NotVulnerable()
    {
        Type.GetType(notTaintedType, null, null, false);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAType_WhenCallingGetTypeTaintedTypeWithFuncAndBooleanAndBoolean_VulnerabilityIsReported()
    {
        Type.GetType(taintedType, null, null, false, false);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAType_WhenCallingGetTypeNotTaintedTypeWithFuncAndBooleanAndBoolean_NotVulnerable()
    {
        Type.GetType(notTaintedType, null, null, false, false);
        AssertNotVulnerable();
    }

    // Tests for all variant of InvokeMember with the name parameter

    [Fact]
    public void GivenAType_WhenCallingInvokeMemberTaintedType_VulnerabilityIsReported()
    {
        typeof(TypeTestsType).InvokeMember(taintedMethod, BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod, null, new TypeTestsType(), null);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAType_WhenCallingInvokeMemberNotTaintedType_NotVulnerable()
    {
        typeof(TypeTestsType).InvokeMember("ToString", BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod, null, new TypeTestsType(), null);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAType_WhenCallingInvokeMemberTaintedTypeWithParametersAndCulture_VulnerabilityIsReported()
    {
        typeof(TypeTestsType).InvokeMember(taintedMethod, BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod, null, new TypeTestsType(), null, null);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAType_WhenCallingInvokeMemberNotTaintedTypeWithParametersAndCulture_NotVulnerable()
    {
        typeof(TypeTestsType).InvokeMember("ToString", BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod, null, new TypeTestsType(), null, null);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAType_WhenCallingInvokeMemberTaintedTypeWithParametersAndModifiers_VulnerabilityIsReported()
    {
        typeof(TypeTestsType).InvokeMember(taintedMethod, BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod, null, new TypeTestsType(), null, null, null, null);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAType_WhenCallingInvokeMemberNotTaintedTypeWithParametersAndModifiers_NotVulnerable()
    {
        typeof(TypeTestsType).InvokeMember("ToString", BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod, null, new TypeTestsType(), null, null, null, null);
        AssertNotVulnerable();
    }
}
