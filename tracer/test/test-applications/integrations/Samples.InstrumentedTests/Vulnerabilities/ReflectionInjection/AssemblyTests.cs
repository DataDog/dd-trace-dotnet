using System;
using System.Reflection;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.ReflectionInjection;

#pragma warning disable CS0618 //  'Assembly.LoadFrom(string, Evidence)' is obsolete: 'This method is obsolete and will be removed in a future release of the .NET Framework.
public class AssemblyTests : InstrumentationTestsBase
{
    protected string taintedAssembly;
    protected string notTaintedAssembly;

    public AssemblyTests()
    {
        taintedAssembly = "path/to/tainted/assembly";
        notTaintedAssembly = "path/to/notTainted/assembly";
        AddTainted(taintedAssembly);
    }

    [Fact]
    public void GivenAnAssembly_LoadTainted_VulnerabilityIsReported()
    {
        try { Assembly.Load(taintedAssembly); } catch { /* ignore */ }
        AssertVulnerable();
    }

    [Fact]
    public void GivenAnAssembly_LoadNotTainted_NotVulnerable()
    {
        try { Assembly.Load(notTaintedAssembly); } catch { /* ignore */ }
        AssertNotVulnerable();
    }

#if NETFRAMEWORK
    [Fact]
    public void GivenAnAssembly_LoadTaintedEvidence_VulnerabilityIsReported()
    {
        try { Assembly.Load(taintedAssembly, null); } catch { /* ignore */ }
        AssertVulnerable();
    }

    [Fact]
    public void GivenAnAssembly_LoadNotTaintedEvidence_NotVulnerable()
    {
        try { Assembly.Load(notTaintedAssembly, null); } catch { /* ignore */ }
        AssertNotVulnerable();
    }
#endif

    [Fact]
    public void GivenAnAssembly_LoadFromTainted_VulnerabilityIsReported()
    {
        try { Assembly.LoadFrom(taintedAssembly); } catch { /* ignore */ }
        AssertVulnerable();
    }

    [Fact]
    public void GivenAnAssembly_LoadFromNotTainted_NotVulnerable()
    {
        try { Assembly.LoadFrom(notTaintedAssembly); } catch { /* ignore */ }
        AssertNotVulnerable();
    }
    
    [Fact]
    public void GivenAnAssembly_LoadFromTaintedHash_VulnerabilityIsReported()
    {
        try { Assembly.LoadFrom(taintedAssembly, null, System.Configuration.Assemblies.AssemblyHashAlgorithm.SHA1); } catch { /* ignore */ }
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAnAssembly_LoadFromNotTaintedHash_NotVulnerable()
    {
        try { Assembly.LoadFrom(notTaintedAssembly, null, System.Configuration.Assemblies.AssemblyHashAlgorithm.SHA1); } catch { /* ignore */ }
        AssertNotVulnerable();
    }
    
#if NETFRAMEWORK
    [Fact]
    public void GivenAnAssembly_LoadFromTaintedEvidenceHash_VulnerabilityIsReported()
    {
        try { Assembly.LoadFrom(taintedAssembly, null, null, System.Configuration.Assemblies.AssemblyHashAlgorithm.SHA1); } catch { /* ignore */ }
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAnAssembly_LoadFromNotTaintedEvidenceHash_NotVulnerable()
    {
        try { Assembly.LoadFrom(notTaintedAssembly, null, null, System.Configuration.Assemblies.AssemblyHashAlgorithm.SHA1); } catch { /* ignore */ }
        AssertNotVulnerable();
    }
    
    [Fact]
    public void GivenAnAssembly_LoadFromTaintedEvidence_VulnerabilityIsReported()
    {
        try { Assembly.LoadFrom(taintedAssembly, null); } catch { /* ignore */ }
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenAnAssembly_LoadFromNotTaintedEvidence_NotVulnerable()
    {
        try { Assembly.LoadFrom(notTaintedAssembly, null); } catch { /* ignore */ }
        AssertNotVulnerable();
    }
#endif

    [Fact]
    public void GivenAnAssembly_AssemblyNameTainted_VulnerabilityIsReported()
    {
        try { Assembly.Load(new AssemblyName(taintedAssembly)); } catch { /* ignore */ }
        AssertVulnerable();
    }

    [Fact]
    public void GivenAnAssembly_AssemblyNameNotTainted_NotVulnerable()
    {
        try { Assembly.Load(new AssemblyName(notTaintedAssembly)); } catch { /* ignore */ }
        AssertNotVulnerable();
    }
}
