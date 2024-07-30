using System;
using System.Data.SqlClient;
using FluentAssertions;
using Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;
using Xunit;

#nullable enable

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.Xss;

public class SqlDbReaderTests : InstrumentationTestsBase, IDisposable
{
    string allPersonsQuery;

    SqlConnection databaseConnection;

    public SqlDbReaderTests()
    {
        databaseConnection = SqlDDBBCreator.Create();
        allPersonsQuery = "SELECT * from Persons";
    }

    public override void Dispose()
    {
        if (databaseConnection != null)
        {
            databaseConnection.Close();
            databaseConnection.Dispose();
            databaseConnection = null;
        }
        base.Dispose();
    }

    [Fact]
    public void GivenASqlReader_WhenCallingGetString_OutputIsTainted()
    {
        using (var reader = TestRealDDBBLocalCall(() => new SqlCommand(allPersonsQuery, databaseConnection).ExecuteReader()))
        {
            if (reader is not null)
            {
                reader.Read().Should().BeTrue();
                {
                    for (int x = 0; x < reader.FieldCount; x++)
                    {
                        if (reader.GetFieldType(x) == typeof(string) && !reader.IsDBNull(x))
                        {
                            var value = reader.GetString(x);
                            AssertTainted(value, $"Reader type : {reader.GetType().FullName} Assembly: {reader.GetType().Assembly.FullName}");
                        }
                    }
                }

                reader.Read().Should().BeTrue();
                {
                    for (int x = 0; x < reader.FieldCount; x++)
                    {
                        if (reader.GetFieldType(x) == typeof(string) && !reader.IsDBNull(x))
                        {
                            var value = reader.GetString(x);
                            AssertNotTainted(value, $"Reader type : {reader.GetType().FullName} Assembly: {reader.GetType().Assembly.FullName}");
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void GivenASqlReader_WhenCallingGetStringAsync_OutputIsTainted()
    {
        using (var reader = TestRealDDBBLocalCall(() => new SqlCommand(allPersonsQuery, databaseConnection).ExecuteReader()))
        {
            if (reader is not null)
            {
                reader.ReadAsync().Result.Should().BeTrue();
                {
                    for (int x = 0; x < reader.FieldCount; x++)
                    {
                        if (reader.GetFieldType(x) == typeof(string) && !reader.IsDBNull(x))
                        {
                            var value = reader.GetString(x);
                            AssertTainted(value, $"Reader type : {reader.GetType().FullName} Assembly: {reader.GetType().Assembly.FullName}");
                        }
                    }
                }

                reader.Read().Should().BeTrue();
                {
                    for (int x = 0; x < reader.FieldCount; x++)
                    {
                        if (reader.GetFieldType(x) == typeof(string) && !reader.IsDBNull(x))
                        {
                            var value = reader.GetString(x);
                            AssertNotTainted(value, $"Reader type : {reader.GetType().FullName} Assembly: {reader.GetType().Assembly.FullName}");
                        }
                    }
                }
            }
        }
    }
}
