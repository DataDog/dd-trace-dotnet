using System.Collections.Generic;
using Moq;
using NHibernate;
using NHibernate.Cfg;
using NHibernate.Criterion.Lambda;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

public class NHibernateTests : InstrumentationTestsBase
{
    private readonly string _taintedValue = "1";
    private readonly Mock<ISession> _session;

    public NHibernateTests()
    {
        AddTainted(_taintedValue);

        _session = new Mock<ISession>();
    }

    private void SetupMockedQuery(string query)
    {
        var queryMock = new Mock<IQuery>();
        queryMock.Setup(x => x.List<string>()).Returns(new List<string>{"test2"} );
        _session.Setup(session => session.CreateQuery(query)).Returns(queryMock.Object);
    }
    
    private void SetupMockedSqlQuery(string query)
    {
        var queryMock = new Mock<ISQLQuery>();
        queryMock.Setup(x => x.List<string>()).Returns(new List<string>{"test2"} );
        _session.Setup(session => session.CreateSQLQuery(query)).Returns(queryMock.Object);
    }

    // With CreateQuery
    [Fact]
    public void GivenHibernate_WhenCallingCreateQueryWithTainted_VulnerabilityIsReported()
    {
        var query = "SELECT * FROM Books WHERE id = " + _taintedValue;

        SetupMockedQuery(query);
        var result = _session.Object.CreateQuery(query).List<string>();

        Assert.NotNull(result);
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenHibernate_WhenCallingCreateQueryWithSafeValue_NoVulnerabilityIsReported()
    {
        var query = "SELECT * FROM Books WHERE id = 2";
        
        SetupMockedQuery(query);
        var result = _session.Object.CreateQuery(query).List<string>();

        Assert.NotNull(result);
        AssertNotVulnerable();
    }
    
    // With CreateSQLQuery
    [Fact]
    public void GivenHibernate_WhenCallingCreateSQLQueryWithTainted_VulnerabilityIsReported()
    {
        var query = "SELECT * FROM Books WHERE id = " + _taintedValue;

        SetupMockedSqlQuery(query);
        var result = _session.Object.CreateSQLQuery(query).List<string>();

        Assert.NotNull(result);
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenHibernate_WhenCallingCreateSQLQueryWithSafeValue_NoVulnerabilityIsReported()
    {
        var query = "SELECT * FROM Books WHERE id = 2";
        
        SetupMockedSqlQuery(query);
        var result = _session.Object.CreateSQLQuery(query).List<string>();

        Assert.NotNull(result);
        AssertNotVulnerable();
    }
}
