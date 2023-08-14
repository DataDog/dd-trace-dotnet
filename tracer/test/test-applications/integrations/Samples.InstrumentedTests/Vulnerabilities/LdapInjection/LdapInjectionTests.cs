using System;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices.Protocols;
using System.Runtime.Versioning;
using Xunit;
using SearchScope = System.DirectoryServices.SearchScope;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.LdapInjection;

#if NET5_0_OR_GREATER
[SupportedOSPlatform("windows")]
#endif
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

    [Trait("Category", "LinuxUnsupported")]
    [Fact]
    public void GivenADirectoryEntry_WhenCreate_LDAPVulnerable()
    {
        _ = new DirectoryEntry(_taintedLdap, "user", "pass");
        AssertVulnerable();
    }

    [Trait("Category", "LinuxUnsupported")]
    [Fact]
    public void GivenADirectoryEntry_WhenCreate_NotLDAPVulnerable()
    {
        _ = new DirectoryEntry(_taintedIIS);
        AssertNotVulnerable();
    }

    [Trait("Category", "LinuxUnsupported")]
    [Fact]
    public void GivenADirectoryEntry_WhenCreate_LDAPVulnerable2()
    {
        Assert.Throws<ArgumentException>(() => new DirectoryEntry((object)_taintedLdap));
        AssertNotVulnerable();
    }

    [Trait("Category", "LinuxUnsupported")]
    [Fact]
    public void GivenADirectoryEntry_WhenCreate_NotLDAPVulnerable3()
    {
        _ = new DirectoryEntry(string.Empty);
        AssertNotVulnerable();
    }

    [Trait("Category", "LinuxUnsupported")]
    [Fact]
    public void GivenADirectoryEntry_WhenCreate_NotLDAPVulnerable4()
    {
        _ = new DirectoryEntry(null);
        AssertNotVulnerable();
    }

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

    [Fact]
    public void GivenAPrincipalContext_WhenCreate_LDAPVulnerable3()
    {
        ExecuteFunc(() => new PrincipalContext(ContextType.Domain, _taintedLdap));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPrincipalContext_WhenCreate_LDAPVulnerable4()
    {
        ExecuteFunc(() => new PrincipalContext(ContextType.Domain, _taintedLdap, "container", ContextOptions.Negotiate));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPrincipalContext_WhenCreate_LDAPVulnerable5()
    {
        ExecuteFunc(() => new PrincipalContext(ContextType.Domain, _untaintedLdap, _taintedContainer, ContextOptions.Negotiate));
        AssertVulnerable();
    }

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

    [Trait("Category", "LinuxUnsupported")]
    [Fact]
    public void GivenADirectorySearcher_WhenCreate_LDAPVulnerable()
    {
        string filter = "(uid=" + _taintedUserAsterisk + ")";
        _ = new DirectorySearcher(filter);
        AssertVulnerable();
    }

    //LdapDirectoryIdentifier

    [Fact]
    public void GivenASearchRequest_WhenCreate_LDAPVulnerable()
    {
        _ = new SearchRequest("name", "user=" + _taintedUserInjection, System.DirectoryServices.Protocols.SearchScope.Subtree, null);
        AssertVulnerable();
    }

    [Fact]
    public void GivenASearchRequest_WhenCreate_LDAPVulnerable2()
    {
        var request = new SearchRequest("name", string.Empty, System.DirectoryServices.Protocols.SearchScope.Subtree, null);
        request.Filter = "user=" + _taintedUserInjection;
        AssertVulnerable();
    }

    //DirectorySearcher LDAPVulnerable: https://docs.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca3005

    [Trait("Category", "LinuxUnsupported")]
    [Fact]
    public void GivenADirectorySearcher_WhenCreate_LDAPVulnerable2()
    {
        var directoryEntry = new DirectoryEntry();
        string filter = "(uid=" + _taintedUserAsterisk + ")";
        _ = new DirectorySearcher(directoryEntry, filter);
        AssertVulnerable();
    }

    [Trait("Category", "LinuxUnsupported")]
    [Fact]
    public void GivenADirectorySearcher_WhenCreate_LDAPVulnerable3()
    {
        var directoryEntry = new DirectoryEntry();
        string filter = "(uid=" + _taintedUserAsterisk + ")";
        _ = new DirectorySearcher(directoryEntry, filter, null);
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectorySearcher_WhenCreate_LDAPVulnerable4()
    {
        string filter = "(uid=" + _taintedUserAsterisk + ")";
        _ = new DirectorySearcher(filter, null);
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectorySearcher_WhenCreate_LDAPVulnerable5()
    {
        string filter = "(uid=" + _taintedUserAsterisk + ")";
        _ = new DirectorySearcher(filter, null, SearchScope.Base);
        AssertVulnerable();
    }

    [Trait("Category", "LinuxUnsupported")]
    [Fact]
    public void GivenADirectorySearcher_WhenCreate_LDAPVulnerable6()
    {
        var directoryEntry = new DirectoryEntry();
        string filter = "(uid=" + _taintedUserAsterisk + ")";
        _ = new DirectorySearcher(directoryEntry, filter, null, SearchScope.Base);
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectorySearcher_WhenCreate_LDAPVulnerable8()
    {
        string filter = "(uid=" + _taintedUserAsterisk + ")";
        var searcher = new DirectorySearcher();
        searcher.Filter = filter;
        AssertVulnerable();
    }

    //Tainted objects should not be used to build the container part of the server address because data cann be injected.
    //https://security.stackexchange.com/questions/101997/c-ldap-injection
    //https://stackoverflow.com/questions/59178153/ldap-injection-vulnerability-with-directoryentry-username-and-password

    [Trait("Category", "LinuxUnsupported")]
    [Fact]
    public void GivenADirectoryEntry_WhenConnectToTaintedServer_LDAPVulnerable()
    {
        string ldapServer = _taintedLdap;
        var entry = new DirectoryEntry();
        entry.Path = ldapServer;
        AssertVulnerable();
    }

    [Trait("Category", "LinuxUnsupported")]
    [Fact]
    public void GivenADirectoryEntry_WhenConnectToTaintedServer_LDAPVulnerable2()
    {
        string ldapServer = _taintedLdap;
        string userName = "cn=read-only-admin,dc=example,dc=com";
        string password = "password";
        _ = new DirectoryEntry(ldapServer, userName, password, AuthenticationTypes.ServerBind);
        AssertVulnerable();
    }

    [Trait("Category", "LinuxUnsupported")]
    [Fact]
    public void GivenADirectoryEntry_WhenConnectToTaintedServer_LDAPVulnerable3()
    {
        string ldapServer = _taintedLdap;
        _ = new DirectoryEntry(ldapServer);
        AssertVulnerable();
    }

    [Trait("Category", "LinuxUnsupported")]
    [Fact]
    public void GivenADirectoryEntry_WhenConnectToTaintedServer_LDAPVulnerable4()
    {
        string ldapServer = _taintedLdap;
        string userName = "cn=read-only-admin,dc=example,dc=com";
        string password = "password";
        _ = new DirectoryEntry(ldapServer, userName, password);
        AssertVulnerable();
    }
}
