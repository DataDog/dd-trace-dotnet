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

    private static readonly Mock<ISessionFactory> SessionFactoryMock = new();
    private static readonly Mock<ISession> SessionMock = new();
    private static readonly Mock<ISQLQuery> SqlQueryMock = new();
    private static ISessionFactory SessionFactory => SessionFactoryMock.Object;

    public NHibernateTests()
    {
        AddTainted(_taintedValue);
        
        SessionFactoryMock.Setup(m => m.OpenSession()).Returns(SessionMock.Object);
        SessionMock.Setup(m => m.CreateSQLQuery(It.IsAny<string>())).Returns(SqlQueryMock.Object);
        SessionMock.Setup(m => m.CreateQuery(It.IsAny<string>())).Returns(SqlQueryMock.Object);
        SqlQueryMock.Setup(m => m.List<string>()).Returns(new List<string>{"test"});
    }

    // With CreateQuery
    [Fact]
    public void GivenHibernate_WhenCallingCreateQueryWithTainted_VulnerabilityIsReported()
    {
        var query = "SELECT * FROM Books WHERE id = " + _taintedValue;

        using var session = SessionFactory.OpenSession();
        var result = session.CreateSQLQuery(query).List<string>();

        Assert.NotNull(result);
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenHibernate_WhenCallingCreateQueryWithSafeValue_NoVulnerabilityIsReported()
    {
        var query = "SELECT * FROM Books WHERE id = 2";
        
        using var session = SessionFactory.OpenSession();
        var result = session.CreateSQLQuery(query).List<string>();

        Assert.NotNull(result);
        AssertNotVulnerable();
    }
    
    // With CreateSQLQuery
    [Fact]
    public void GivenHibernate_WhenCallingCreateSQLQueryWithTainted_VulnerabilityIsReported()
    {
        var query = "SELECT * FROM Books WHERE id = " + _taintedValue;

        using var session = SessionFactory.OpenSession();
        var result = session.CreateSQLQuery(query).List<string>();

        Assert.NotNull(result);
        AssertVulnerable();
    }
    
    [Fact]
    public void GivenHibernate_WhenCallingCreateSQLQueryWithSafeValue_NoVulnerabilityIsReported()
    {
        var query = "SELECT * FROM Books WHERE id = 2";
        
        using var session = SessionFactory.OpenSession();
        var result = session.CreateSQLQuery(query).List<string>();

        Assert.NotNull(result);
        AssertNotVulnerable();
    }
}
