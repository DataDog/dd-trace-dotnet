using System;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices.Protocols;
using System.Runtime.Versioning;
using Xunit;
using SearchScope = System.DirectoryServices.SearchScope;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.LdapInjection;

// Ldap injection can happen in the DN or the filter. More information:
// https://cheatsheetseries.owasp.org/cheatsheets/LDAP_Injection_Prevention_Cheat_Sheet.html

#if NET5_0_OR_GREATER
[SupportedOSPlatform("windows")]
#endif
[Trait("Category", "LinuxUnsupported")]
public class LdapInjectionTests : InstrumentationTestsBase
{
    private string _taintedUser = "user";
    private string _taintedUserAsterisk = "*";
    private string _taintedUserInjection = "*)(uid=*))(|(uid=*";
    private string _taintedLdap = @"LDAP://fakeorg, DC=com";
    private string _untaintedLdap = @"LDAP://fakeorgUntainted, DC=com";
    private string _taintedIIS = @"IIS://LocalHost/W3SVC/1/ROOT/testbedweb";
    private string _taintedContainer = "CN=johndoe,OU=Users,OU=VSMGUI,DC=yourfirm,DC=com";

    public LdapInjectionTests()
    {
        AddTainted(_taintedLdap);
        AddTainted(_taintedUser);
        AddTainted(_taintedUserAsterisk);
        AddTainted(_taintedUserInjection);
        AddTainted(_taintedIIS);
        AddTainted(_taintedContainer);
    }

    // Not Vulnerable overloads

    [Fact]
    public void GivenADirectoryEntry_WhenCreate_LDAPVulnerable2()
    {
        Assert.Throws<ArgumentException>(() => new DirectoryEntry((object)_taintedLdap));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPrincipalContext_WhenCreate_LDAPVulnerable3()
    {
        ExecuteFunc(() => new PrincipalContext(ContextType.Domain, _taintedLdap));
        AssertNotVulnerable();
    }

    // Testing [AspectMethodInsertBefore("System.DirectoryServices.DirectoryEntry::set_Path(System.String)")]

    [Fact]
    public void GivenADirectoryEntry_WhenConnectToTaintedServer_LDAPVulnerable()
    {
        var entry = new DirectoryEntry();
        entry.Path = _taintedLdap;
        AssertVulnerable();
    }

    // Testing [AspectMethodInsertBefore("System.DirectoryServices.DirectoryEntry::.ctor(System.String)")]

    [Fact]
    public void GivenADirectoryEntry_WhenConnectToTaintedServer_LDAPVulnerable3()
    {
        _ = new DirectoryEntry(_taintedLdap);
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryEntry_WhenCreate_NotLDAPVulnerable()
    {
        _ = new DirectoryEntry(_taintedIIS);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenADirectoryEntry_WhenCreate_NotLDAPVulnerable3()
    {
        _ = new DirectoryEntry(string.Empty);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenADirectoryEntry_WhenCreate_NotLDAPVulnerable4()
    {
        _ = new DirectoryEntry(null);
        AssertNotVulnerable();
    }

    // Testing [AspectMethodInsertBefore("System.DirectoryServices.DirectoryEntry::.ctor(System.String,System.String,System.String)", 2)]

    [Fact]
    public void GivenADirectoryEntry_WhenCreate_LDAPVulnerable()
    {
        _ = new DirectoryEntry(_taintedLdap, "user", "pass");
        AssertVulnerable();
    }

    // Testing [AspectMethodInsertBefore("System.DirectoryServices.DirectoryEntry::.ctor(System.String,System.String,System.String,System.DirectoryServices.AuthenticationTypes)", 3)]

    [Fact]
    public void GivenADirectoryEntry_WhenConnectToTaintedServer_LDAPVulnerable2()
    {
        var userName = "cn=read-only-admin,dc=example,dc=com";
        var password = "password";
        _ = new DirectoryEntry(_taintedLdap, userName, password, AuthenticationTypes.ServerBind);
        AssertVulnerable();
    }

    // Testing [AspectMethodInsertBefore("System.DirectoryServices.DirectorySearcher::set_Filter(System.String)")]

    [Fact]
    public void GivenADirectorySearcher_WhenCreate_LDAPVulnerable8()
    {
        var filter = "(uid=" + _taintedUserAsterisk + ")";
        var searcher = new DirectorySearcher();
        searcher.Filter = filter;
        AssertVulnerable();
    }

    // Testing [AspectMethodInsertBefore("System.DirectoryServices.DirectorySearcher::.ctor(System.String)")]

    [Fact]
    public void GivenADirectorySearcher_WhenCreate_LDAPVulnerable()
    {
        var filter = "(uid=" + _taintedUserAsterisk + ")";
        _ = new DirectorySearcher(filter);
        AssertVulnerable();
    }

    // Testing [AspectMethodInsertBefore("System.DirectoryServices.DirectorySearcher::.ctor(System.String,System.String[])", 1)]

    [Fact]
    public void GivenADirectorySearcher_WhenCreate_LDAPVulnerable4()
    {
        var filter = "(uid=" + _taintedUserAsterisk + ")";
        _ = new DirectorySearcher(filter, null);
        AssertVulnerable();
    }

    // Testing [AspectMethodInsertBefore("System.DirectoryServices.DirectorySearcher::.ctor(System.String,System.String[],System.DirectoryServices.SearchScope)", 2)]

    [Fact]
    public void GivenADirectorySearcher_WhenCreate_LDAPVulnerable5()
    {
        var filter = "(uid=" + _taintedUserAsterisk + ")";
        _ = new DirectorySearcher(filter, null, SearchScope.Base);
        AssertVulnerable();
    }

    // Testing [AspectMethodInsertBefore("System.DirectoryServices.DirectorySearcher::.ctor(System.DirectoryServices.DirectoryEntry,System.String)")]

    [Fact]
    public void GivenADirectorySearcher_WhenCreate_LDAPVulnerable2()
    {
        var directoryEntry = new DirectoryEntry();
        var filter = "(uid=" + _taintedUserAsterisk + ")";
        _ = new DirectorySearcher(directoryEntry, filter);
        AssertVulnerable();
    }

    // Testing [AspectMethodInsertBefore("System.DirectoryServices.DirectorySearcher::.ctor(System.DirectoryServices.DirectoryEntry,System.String,System.String[])", 1)]

    [Fact]
    public void GivenADirectorySearcher_WhenCreate_LDAPVulnerable3()
    {
        var directoryEntry = new DirectoryEntry();
        var filter = "(uid=" + _taintedUserAsterisk + ")";
        _ = new DirectorySearcher(directoryEntry, filter, null);
        AssertVulnerable();
    }

    // Testing [AspectMethodInsertBefore("System.DirectoryServices.DirectorySearcher::.ctor(System.DirectoryServices.DirectoryEntry,System.String,System.String[],System.DirectoryServices.SearchScope)", 2)]

    [Fact]
    public void GivenADirectorySearcher_WhenCreate_LDAPVulnerable6()
    {
        var directoryEntry = new DirectoryEntry();
        var filter = "(uid=" + _taintedUserAsterisk + ")";
        _ = new DirectorySearcher(directoryEntry, filter, null, SearchScope.Base);
        AssertVulnerable();
    }

    // Testing [AspectMethodInsertBefore("System.DirectoryServices.AccountManagement.PrincipalContext::.ctor(System.DirectoryServices.AccountManagement.ContextType,System.String,System.String)")]

    [Fact]
    public void GivenAPrincipalContext_WhenCreate_LDAPVulnerable()
    {
        ExecuteFunc(() => new PrincipalContext(ContextType.Domain, _taintedLdap, "dc=example,dc=com"));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPrincipalContext_WhenCreate_LDAPVulnerable2()
    {
        ExecuteFunc(() => new PrincipalContext(ContextType.Domain, "ldap:\\myserver", _taintedContainer));
        AssertVulnerable();
    }

    // Testing [AspectMethodInsertBefore("System.DirectoryServices.AccountManagement.PrincipalContext::.ctor(System.DirectoryServices.AccountManagement.ContextType,System.String,System.String,System.String)", 1)]

    [Fact]
    public void GivenAPrincipalContext_WhenCreate_LDAPVulnerable6()
    {
        ExecuteFunc(() => new PrincipalContext(ContextType.Domain, _taintedLdap, "container", "password"));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPrincipalContext_WhenCreate_LDAPVulnerable7()
    {
        ExecuteFunc(() => new PrincipalContext(ContextType.Domain, _untaintedLdap, _taintedContainer, "password"));
        AssertVulnerable();
    }

    // Testing [AspectMethodInsertBefore("System.DirectoryServices.AccountManagement.PrincipalContext::.ctor(System.DirectoryServices.AccountManagement.ContextType,System.String,System.String,System.String,System.String)", 2)]

    [Fact]
    public void GivenAPrincipalContext_WhenCreate_LDAPVulnerable8()
    {
        ExecuteFunc(() => new PrincipalContext(ContextType.Domain, _taintedLdap, "container", "user", "password"));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPrincipalContext_WhenCreate_LDAPVulnerable9()
    {
        ExecuteFunc(() => new PrincipalContext(ContextType.Domain, _untaintedLdap, _taintedContainer, "user", "password"));
        AssertVulnerable();
    }

    // Testing [AspectMethodInsertBefore("System.DirectoryServices.AccountManagement.PrincipalContext::.ctor(System.DirectoryServices.AccountManagement.ContextType,System.String,System.String,System.DirectoryServices.AccountManagement.ContextOptions)", 1)]

    [Fact]
    public void GivenAPrincipalContext_WhenCreate_LDAPVulnerable13()
    {
        ExecuteFunc(() => new PrincipalContext(ContextType.Domain, _taintedLdap, "container", ContextOptions.Negotiate));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPrincipalContext_WhenCreate_LDAPVulnerable12()
    {
        ExecuteFunc(() => new PrincipalContext(ContextType.Domain, _untaintedLdap, _taintedContainer, ContextOptions.Negotiate));
        AssertVulnerable();
    }

    // Testing [AspectMethodInsertBefore("System.DirectoryServices.AccountManagement.PrincipalContext::.ctor(System.DirectoryServices.AccountManagement.ContextType,System.String,System.String,System.DirectoryServices.AccountManagement.ContextOptions,System.String,System.String)", 3)]

    [Fact]
    public void GivenAPrincipalContext_WhenCreate_LDAPVulnerable10()
    {
        ExecuteFunc(() => new PrincipalContext(ContextType.Domain, _taintedLdap, "container", ContextOptions.Negotiate, "user", "password"));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPrincipalContext_WhenCreate_LDAPVulnerable11()
    {
        ExecuteFunc(() => new PrincipalContext(ContextType.Domain, _untaintedLdap, _taintedContainer, ContextOptions.Negotiate, "user", "password"));
        AssertVulnerable();
    }

    // Testing [AspectMethodInsertBefore("System.DirectoryServices.Protocols.SearchRequest::.ctor(System.String,System.String,System.DirectoryServices.Protocols.SearchScope,System.String[])", 2)]

    [Fact]
    public void GivenASearchRequest_WhenCreate_LDAPVulnerable()
    {
        _ = new SearchRequest("name", "user=" + _taintedUserInjection, System.DirectoryServices.Protocols.SearchScope.Subtree, null);
        AssertVulnerable();
    }

    // Testing [AspectMethodInsertBefore("System.DirectoryServices.Protocols.SearchRequest::set_Filter(System.Object)")]

    [Fact]
    public void GivenASearchRequest_WhenCreate_LDAPVulnerable2()
    {
        var request = new SearchRequest("name", string.Empty, System.DirectoryServices.Protocols.SearchScope.Subtree, null);
        request.Filter = "user=" + _taintedUserInjection;
        AssertVulnerable();
    }
}
