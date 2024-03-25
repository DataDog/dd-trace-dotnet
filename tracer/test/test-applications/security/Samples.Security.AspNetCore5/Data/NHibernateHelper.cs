using System.Collections.Generic;
using Moq;
using NHibernate;

namespace Samples.Security.Data;

public abstract class NHibernateHelper
{
    private static readonly Mock<ISessionFactory> SessionFactoryMock = new();
    private static readonly Mock<ISession> SessionMock = new();
    private static readonly Mock<ISQLQuery> SqlQueryMock = new();

    private static ISessionFactory SessionFactory => SessionFactoryMock.Object;

    static NHibernateHelper()
    {
        SessionFactoryMock.Setup(m => m.OpenSession()).Returns(SessionMock.Object);
        SessionMock.Setup(m => m.CreateSQLQuery(It.IsAny<string>())).Returns(SqlQueryMock.Object);
        SqlQueryMock.Setup(m => m.List<string>()).Returns(new List<string>{"username_test"});
    }

    // Execute a Select query on the nhibernate database
    public static IList<string> CreateSqlQuery(string sqlQuery)
    {
        using var session = SessionFactory.OpenSession();
        return session.CreateSQLQuery(sqlQuery).List<string>();
    }
}
