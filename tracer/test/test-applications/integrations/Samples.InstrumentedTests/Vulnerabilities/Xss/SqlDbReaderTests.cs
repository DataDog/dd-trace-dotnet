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
    public void GivenASqlReader_WhenReadingStringValues_OutputIsTainted()
    {
        using (var reader = TestRealDDBBLocalCall(() => new SqlCommand(allPersonsQuery, databaseConnection).ExecuteReader()))
        {
            if (reader is not null)
            {
                reader.Read().Should().BeTrue();
                // @"INSERT INTO Persons (Id, Name, Surname, Married, Gender, DateOfBirth, Email, FlattedAddress, Mobile, NIF, Phone, PostalCode, Details, CityIniqueId, CountryUniqueId, ImagePath) VALUES ('D305C1EB-B72E-4340-B5BA-A19D0105A6C2', 'Michael', 'Smith', 0, 'Female    ', NULL, 'Michael.Smith@gmail.com', 'Mountain Avenue 55', '650214751', '50099554L', '918084525', '28341', 'Not Defined', NULL, NULL, '~/Images/Antonio.jpg')",

                if (!reader.IsDBNull(1)) //Name
                {
                    var value = reader.GetString(1);
                    AssertTainted(value);
                }

                if (!reader.IsDBNull(2)) //Surname
                {
                    var value = reader.GetValue(2);
                    AssertTainted(value);
                }

                if (!reader.IsDBNull(6)) //Email
                {
                    var value = reader[6];
                    AssertTainted(value);
                }

                if (!reader.IsDBNull(12)) //Details
                {
                    var value = reader["Details"];
                    AssertTainted(value);
                }
            }
        }
    }

    [Fact]
    public void GivenASqlReader_WhenUsingRead_OutputIsTainted()
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
    public void GivenASqlReader_WhenUsingReadAsync_OutputIsTainted()
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
