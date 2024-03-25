using System.Collections.Generic;
using Moq;
using NHibernate;
using NHibernate.Cfg;
using NHibernate.Criterion.Lambda;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

public class HibernateTests : InstrumentationTestsBase
{
    private readonly string _taintedValue = "1";
    private readonly ISession _session;

    public HibernateTests()
    {
        AddTainted(_taintedValue);

        // Mocking the NHibernate session
        var mockSessionFactory = new Mock<ISessionFactory>();
        var mockSession = new Mock<ISession>();
        mockSessionFactory.Setup(factory => factory.OpenSession()).Returns(mockSession.Object);
        _session = mockSessionFactory.Object.OpenSession();
    }

    [Fact]
    public void GivenHibernate_WhenCallingCreateQueryWithTainted_VulnerabilityIsReported()
    {
        var query = "SELECT * FROM Books WHERE id = " + _taintedValue;

        var sessionMock = new Mock<ISession>();
        var queryMock = new Mock<IQuery>();
        var transactionMock = new Mock<ITransaction>();

        _session.CreateQuery("lol");
    
        sessionMock.SetupGet(x => x.Transaction).Returns(transactionMock.Object);
        sessionMock.Setup(session => session.CreateQuery("from User")).Returns(queryMock.Object);
        
        sessionMock.Setup(session => session.BeginTransaction()).Returns(transactionMock.Object);
        sessionMock.Setup(session => session.CreateQuery(query)).Returns(queryMock.Object);
        sessionMock.Setup(session => session.Close());
        queryMock.Setup(x => x.List<string>()).Returns(new List<string>{"test2"} );
        
        var result = sessionMock.Object.CreateQuery(query).List<string>();

        Assert.NotNull(result);
        AssertVulnerable();
    }

}
