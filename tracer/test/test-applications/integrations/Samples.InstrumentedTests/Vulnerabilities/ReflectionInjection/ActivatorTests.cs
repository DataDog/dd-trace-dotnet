using System;
using System.Globalization;
using System.Reflection;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.ReflectionInjection;

public class ActivatorTestType;

public class ActivatorTests : InstrumentationTestsBase
{
    protected string assembly;
    protected string assemblyFile;
    protected string notTaintedType;

    protected string assemblyTainted;
    protected string typeTainted;

    public ActivatorTests()
    {
        assembly = Assembly.GetExecutingAssembly().GetName().Name;
        assemblyFile = Assembly.GetExecutingAssembly().Location;
        notTaintedType = GetType().BaseType!.FullName;

        typeTainted = typeof(ActivatorTestType).FullName;
        assemblyTainted = typeof(string).Assembly.GetName().Name;

        AddTainted(assemblyTainted);
        AddTainted(typeTainted);
    }

#if !NETCOREAPP2_1
    [Fact]
    public void GivenAnActivator_WhenCallingCreateInstance2TaintedType_VulnerabilityIsReported()
    {
        Activator.CreateInstance(assembly, typeTainted);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAnActivator_WhenCallingCreateInstance2TaintedAssembly_VulnerabilityIsReported()
    {
        var type = typeof(DateTime).FullName;
        Activator.CreateInstance(assemblyTainted, type);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAnActivator_WhenCallingCreateInstanceTaintedAssembly_VulnerabilityIsReported()
    {
        AddTainted(assembly);
        Activator.CreateInstance(assembly, notTaintedType);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAnActivator_WhenCallingCreateInstanceTaintedTypeAssembly_VulnerabilityIsReported()
    {
        AddTainted(assembly);
        Activator.CreateInstance(assembly, typeTainted);
        AssertVulnerable(vulnerabilities: 2);
    }

    [Fact]
    public void GivenAnActivator_WhenCallingCreateInstanceNotTainted_VulnerabilityIsNotReported()
    {
        Activator.CreateInstance(assembly, notTaintedType);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAnActivator_WhenCallingCreateInstanceTaintedTypeNullParameters_VulnerabilityIsReported()
    {
        Activator.CreateInstance(assembly, typeTainted, activationAttributes: null);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAnActivator_WhenCallingCreateInstanceTaintedTypeParameters_VulnerabilityIsReported()
    {
        Activator.CreateInstance(assembly, typeTainted, new object[] { });
        AssertVulnerable();
    }
#endif

#if NETFRAMEWORK
    [Fact]
    public void GivenAnActivator_WhenCallingCreateInstanceTaintedTypeFlagsCulture_VulnerabilityIsReported()
    {
        Activator.CreateInstance(assembly, typeTainted, ignoreCase: true, BindingFlags.Default, binder: null, args: null, CultureInfo.InvariantCulture, activationAttributes: null);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAnActivator_WhenCallingCreateInstanceDomainTaintedType_VulnerabilityIsReported()
    {
        Activator.CreateInstance(AppDomain.CurrentDomain, assembly, typeTainted);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAnActivator_WhenCallingCreateInstanceDomainTaintedTypeCulture_VulnerabilityIsReported()
    {
        Activator.CreateInstance(
            AppDomain.CurrentDomain,
            assembly,
            typeTainted,
            true,
            BindingFlags.Default,
            null,
            new object[] { },
            CultureInfo.InvariantCulture,
            new object[] { });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAnActivator_WhenCallingCreateInstanceFrom_VulnerabilityIsReported6()
    {
        Activator.CreateInstanceFrom(AppDomain.CurrentDomain, assemblyFile, typeTainted);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAnActivator_WhenCallingCreateInstanceFromDomainTaintedType_VulnerabilityIsReported8()
    {
        Activator.CreateInstanceFrom(
            AppDomain.CurrentDomain,
            assemblyFile,
            typeTainted,
            true,
            BindingFlags.Default,
            null,
            new object[] { },
            CultureInfo.InvariantCulture,
            new object[] { });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAnActivator_WhenCallingCreateComInstanceFromTaintedType_VulnerabilityIsReported()
    {
        Activator.CreateComInstanceFrom(assemblyFile, typeTainted);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAnActivator_WhenCallingCreateComInstanceFromDomainTaintedAssembly_VulnerabilityIsReported()
    {
        AddTainted(assemblyFile);
        Activator.CreateComInstanceFrom(assemblyFile, notTaintedType);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAnActivator_WhenCallingCreateComInstanceFromTaintedTypeAssembly_VulnerabilityIsReported()
    {
        AddTainted(assemblyFile);
        Activator.CreateComInstanceFrom(assemblyFile, typeTainted);
        AssertVulnerable(vulnerabilities: 2);
    }

    [Fact]
    public void GivenAnActivator_WhenCallingCreateComInstanceFromNotTainted_VulnerabilityIsNotReported()
    {
        Activator.CreateComInstanceFrom(assemblyFile, notTaintedType);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAnActivator_WhenCallingCreateComInstanceHashFromTainted_VulnerabilityIsReported()
    {
        AddTainted(assemblyFile);
        Activator.CreateComInstanceFrom(assemblyFile, notTaintedType, null, System.Configuration.Assemblies.AssemblyHashAlgorithm.MD5);
        AssertVulnerable();
    }
#endif
    
#if !NETCOREAPP2_1
    [Fact]
    public void GivenAnActivator_WhenCallingCreateInstanceFromTaintedType_VulnerabilityIsReported()
    {
        Activator.CreateInstanceFrom(assemblyFile, typeTainted);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAnActivator_WhenCallingCreateInstanceFromTaintedAssembly_VulnerabilityIsReported2()
    {
        AddTainted(assemblyFile);
        Activator.CreateInstanceFrom(assemblyFile, notTaintedType);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAnActivator_WhenCallingCreateInstanceFromTaintedAssemblyCulture_VulnerabilityIsReported3()
    {
        AddTainted(assemblyFile);
        Activator.CreateInstanceFrom(assemblyFile, typeTainted);
        AssertVulnerable(vulnerabilities: 2);
    }

    [Fact]
    public void GivenAnActivator_WhenCallingCreateInstanceFromNotTainted_VulnerabilityIsNotReported()
    {
        Activator.CreateInstanceFrom(assemblyFile, notTaintedType);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAnActivator_WhenCallingCreateInstanceFromTaintedTypeNullParameters_VulnerabilityIsReported()
    {
        Activator.CreateInstanceFrom(assemblyFile, typeTainted, activationAttributes: null);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAnActivator_WhenCallingCreateInstanceFromTaintedTypeParameters_VulnerabilityIsReported()
    {
        Activator.CreateInstanceFrom(assemblyFile, typeTainted, new object[] { });
        AssertVulnerable();
    }
#endif
#if NETFRAMEWORK
    [Fact]
    public void GivenAnActivator_WhenCallingCreateInstanceFromTaintedTypeCulture_VulnerabilityIsReported()
    {
        Activator.CreateInstanceFrom(
            assemblyFile,
            typeTainted,
            ignoreCase: true,
            BindingFlags.Default,
            binder: null,
            new object[] { },
            CultureInfo.InvariantCulture,
            new object[] { });

        AssertVulnerable();
    }
#endif
}
