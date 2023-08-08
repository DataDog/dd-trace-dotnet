using System;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices.Protocols;
using System.Linq;
using System.Net;
using System.Runtime.Versioning;
using FluentAssertions;
using Xunit;
using SearchScope = System.DirectoryServices.SearchScope;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.LdapInjection;

#if NET5_0_OR_GREATER
[SupportedOSPlatform("windows")]
#endif
//[Trait("Category", "LinuxUnsupported")]
public class LdapInjectionTests : InstrumentationTestsBase
{
    protected string taintedUser = "user";
    protected string taintedUserAsterisk = "*";
    protected string taintedUserInjection = "*)(uid=*))(|(uid=*";
    protected string taintedLdap = @"LDAP://MCBcorp, DC=com";
    protected string taintedIIS = @"IIS://LocalHost/W3SVC/1/ROOT/testbedweb";
    protected string taintedContainer = "CN=johndoe,OU=Users,OU=VSMGUI,DC=yourfirm,DC=com";

    public LdapInjectionTests()
    {
        AddTainted(taintedLdap);
        AddTainted(taintedUser);
        AddTainted(taintedUserAsterisk);
        AddTainted(taintedUserInjection);
        AddTainted(taintedIIS);
        AddTainted(taintedContainer);
    }

    [Fact]
    public void GivenADirectoryEntry_WhenCreate_LDAPVulnerable()
    {
        _ = new DirectoryEntry(taintedLdap, "user", "pass");
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryEntry_WhenCreate_NotLDAPVulnerable()
    {
        _ = new DirectoryEntry(taintedIIS);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenADirectoryEntry_WhenCreate_NotLDAPVulnerable2()
    {
        _ = new DirectoryEntry(string.Empty);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenADirectoryEntry_WhenCreate_NotLDAPVulnerable3()
    {
        _ = new DirectoryEntry(null);
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenADirectoryEntry_WhenMakeRealConnection_LDAPVulnerable()
    {
        DirectoryEntry entry = new DirectoryEntry("LDAP://ldap.forumsys.com:389/dc=example,dc=com", "", "", AuthenticationTypes.None);
        DirectorySearcher search = new DirectorySearcher(entry);
        search.Filter = "(uid=" + taintedUserAsterisk + ")";
        search.PropertiesToLoad.Add("*");
        var result = search.FindAll();
        result.Count.Should().BeGreaterThan(0);
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryEntry_WhenMakeRealConnection_LDAPVulnerable2()
    {
        string ldapServer = "LDAP://ldap.forumsys.com:389/dc=example,dc=com";
        string userName = "cn=read-only-admin,dc=example,dc=com";
        string password = "password";
        var directoryEntry = new DirectoryEntry(ldapServer, userName, password, AuthenticationTypes.ServerBind); // Bind to server with admin. Real life should use a service user. 
        object obj = directoryEntry.NativeObject;
        if (obj == null)
        {
            Console.WriteLine("Bind with admin failed!.");
            Environment.Exit(1);
        }
        else
        {
            Console.WriteLine("Bind with admin succeeded!");
        }
        // Search for the user first. 
        DirectorySearcher searcher = new DirectorySearcher(directoryEntry);
        searcher.Filter = "(uid=" + taintedUserAsterisk + ")";
        searcher.PropertiesToLoad.Add("*");
        _ = searcher.FindOne();
        // First we should handle user not found. 
        // To simplify, skip it and try to bind to the user. 
        DirectoryEntry validator = new DirectoryEntry(ldapServer, "uid=riemann,dc=example,dc=com", password, AuthenticationTypes.ServerBind);
        if (validator.NativeObject.Equals(null))
        {
            Console.WriteLine("Cannot bind to user!");
        }
        else
        {
            Console.WriteLine("Bind with user succeeded!");
        }
        AssertVulnerable();
    }

    [Fact]
    public void GivenAPrincipalContext_WhenMakeRealConnection_LDAPVulnerable()
    {
        //using (_ = new PrincipalContext(ContextType.Domain, "ldap.forumsys.com:389", "dc=example,dc=com", "cn=read-only-admin,dc=example,dc=com", "password"))
        using (var ctx = new PrincipalContext(ContextType.Domain, "ldap.forumsys.com:389", "dc=example,dc=com"))
        {
            using (var searcher = new PrincipalSearcher(new UserPrincipal(ctx)))
            {
                foreach (var result in searcher.FindAll().Take(1))
                {
                    _ = result.GetUnderlyingObject() as DirectoryEntry;
                }
            }
        }
        AssertVulnerable();
    }

    [Fact]
    public void GivenAPrincipalContext_WhenCreate_LDAPVulnerable()
    {
        try
        {
            _ = new PrincipalContext(ContextType.Domain, taintedLdap, "dc=example,dc=com");
        }
        catch
        {
            //Cannot connect to non existing server
        }
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPrincipalContext_WhenCreate_LDAPVulnerable2()
    {
        try
        {
            _ = new PrincipalContext(ContextType.Domain, "ldap:\\myserver", taintedContainer);
        }
        catch
        {
            //Cannot connect to non existing server
        }
        AssertVulnerable();
    }

    [Fact]
    public void GivenAPrincipalContext_WhenCreate_LDAPVulnerable3()
    {
        try
        {
            _ = new PrincipalContext(ContextType.Domain, taintedLdap);
        }
        catch
        {
            //Cannot connect to non existing server
        }
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPrincipalContext_WhenCreate_LDAPVulnerable4()
    {
        try
        {
            _ = new PrincipalContext(ContextType.Domain, taintedLdap, "container", ContextOptions.Negotiate);
        }
        catch
        {
            //Cannot connect to non existing server
        }
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPrincipalContext_WhenCreate_LDAPVulnerable5()
    {
        try
        {
            _ = new PrincipalContext(ContextType.Domain, "taintedLdap", taintedContainer, ContextOptions.Negotiate);
        }
        catch
        {
            //Cannot connect to non existing server
        }
        AssertVulnerable();
    }

    [Fact]
    public void GivenAPrincipalContext_WhenCreate_LDAPVulnerable6()
    {
        try
        {
            _ = new PrincipalContext(ContextType.Domain, taintedLdap, "container", "password");
        }
        catch
        {
            //Cannot connect to non existing server
        }
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPrincipalContext_WhenCreate_LDAPVulnerable7()
    {
        try
        {
            _ = new PrincipalContext(ContextType.Domain, "taintedLdap", taintedContainer, "password");
        }
        catch
        {
            //Cannot connect to non existing server
        }
        AssertVulnerable();
    }

    [Fact]
    public void GivenAPrincipalContext_WhenCreate_LDAPVulnerable8()
    {
        try
        {
            _ = new PrincipalContext(ContextType.Domain, taintedLdap, "container", "user", "password");
        }
        catch
        {
            //Cannot connect to non existing server
        }
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPrincipalContext_WhenCreate_LDAPVulnerable9()
    {
        try
        {
            _ = new PrincipalContext(ContextType.Domain, "taintedLdap", taintedContainer, "user", "password");
        }
        catch
        {
            //Cannot connect to non existing server
        }
        AssertVulnerable();
    }

    [Fact]
    public void GivenAPrincipalContext_WhenCreate_LDAPVulnerable10()
    {
        try
        {
            _ = new PrincipalContext(ContextType.Domain, taintedLdap, "container", ContextOptions.Negotiate, "user", "password");
        }
        catch
        {
            //Cannot connect to non existing server
        }
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPrincipalContext_WhenCreate_LDAPVulnerable11()
    {
        try
        {
            _ = new PrincipalContext(ContextType.Domain, "taintedLdap", taintedContainer, ContextOptions.Negotiate, "user", "password");
        }
        catch
        {
            //Cannot connect to non existing server
        }
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectorySearcher_WhenCreate_LDAPVulnerable()
    {
        string filter = "(uid=" + taintedUserAsterisk + ")";
        _ = new DirectorySearcher(filter);
        AssertVulnerable();
    }

    [Fact]
    public void GivenALdapConnection_WhenCreate_LDAPVulnerable()
    {
        string server = "ldap.forumsys.com:389";
        string userName = "uid=tesla,dc=example,dc=com";
        string password = "password";

        using (LdapConnection connection = new LdapConnection(server))
        {
            connection.Timeout = new TimeSpan(0, 0, 10);
            connection.AuthType = AuthType.Basic;
            connection.SessionOptions.ProtocolVersion = 3; // Set protocol to LDAPv3

            var credential = new NetworkCredential(userName, password);
            connection.Bind(credential);
        }
        AssertVulnerable();
    }

    //LdapDirectoryIdentifier

    [Fact]
    public void GivenASearchRequest_WhenCreate_LDAPVulnerable()
    {
        _ = new SearchRequest("name", "user=" + taintedUserInjection, System.DirectoryServices.Protocols.SearchScope.Subtree, null);
        AssertVulnerable();
    }

    //DirectorySearcher LDAPVulnerable: https://docs.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca3005

    [Fact]
    public void GivenADirectorySearcher_WhenCreate_LDAPVulnerable2()
    {
        var directoryEntry = new DirectoryEntry();
        string filter = "(uid=" + taintedUserAsterisk + ")";
        _ = new DirectorySearcher(directoryEntry, filter);
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectorySearcher_WhenCreate_LDAPVulnerable3()
    {
        var directoryEntry = new DirectoryEntry();
        string filter = "(uid=" + taintedUserAsterisk + ")";
        _ = new DirectorySearcher(directoryEntry, filter, null);
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectorySearcher_WhenCreate_LDAPVulnerable4()
    {
        string filter = "(uid=" + taintedUserAsterisk + ")";
        _ = new DirectorySearcher(filter, null);
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectorySearcher_WhenCreate_LDAPVulnerable5()
    {
        string filter = "(uid=" + taintedUserAsterisk + ")";
        _ = new DirectorySearcher(filter, null, SearchScope.Base);
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectorySearcher_WhenCreate_LDAPVulnerable6()
    {
        var directoryEntry = new DirectoryEntry();
        string filter = "(uid=" + taintedUserAsterisk + ")";
        _ = new DirectorySearcher(directoryEntry, filter, null, SearchScope.Base);
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectorySearcher_WhenCreate_LDAPVulnerable8()
    {
        string filter = "(uid=" + taintedUserAsterisk + ")";
        var searcher = new DirectorySearcher();
        searcher.Filter = filter;
        AssertVulnerable();
    }

    //Tainted objects should not be used to build the container part of the server address because data cann be injected.
    //https://security.stackexchange.com/questions/101997/c-ldap-injection
    //https://stackoverflow.com/questions/59178153/ldap-injection-vulnerability-with-directoryentry-username-and-password

    [Fact]
    public void GivenADirectoryEntry_WhenConnectToTaintedServer_LDAPVulnerable()
    {
        string ldapServer = taintedLdap;
        var entry = new DirectoryEntry();
        entry.Path = ldapServer;
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryEntry_WhenConnectToTaintedServer_LDAPVulnerable2()
    {
        string ldapServer = taintedLdap;
        string userName = "cn=read-only-admin,dc=example,dc=com";
        string password = "password";
        _ = new DirectoryEntry(ldapServer, userName, password, AuthenticationTypes.ServerBind);
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryEntry_WhenConnectToTaintedServer_LDAPVulnerable3()
    {
        string ldapServer = taintedLdap;
        _ = new DirectoryEntry(ldapServer);
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryEntry_WhenConnectToTaintedServer_LDAPVulnerable4()
    {
        string ldapServer = taintedLdap;
        string userName = "cn=read-only-admin,dc=example,dc=com";
        string password = "password";
        _ = new DirectoryEntry(ldapServer, userName, password);
        AssertVulnerable();
    }
}
