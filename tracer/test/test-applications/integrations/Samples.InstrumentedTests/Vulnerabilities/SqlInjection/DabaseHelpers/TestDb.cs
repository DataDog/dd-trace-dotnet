using LinqToDB;
using LinqToDB.Data;
using LinqToDB.Mapping;
using ColumnAttribute = LinqToDB.Mapping.ColumnAttribute;
using TableAttribute = LinqToDB.Mapping.TableAttribute;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

public class TestDb : DataConnection
{
    public TestDb(string connectionString) : base(LinqToDB.DataProvider.SqlServer.SqlServerTools.GetDataProvider(), connectionString)
    {
    }

    [Table(Schema = "dbo", Name = "Persons")]
    public partial class Person
    {
        [Column, NotNull] public string Name { get; set; } // nvarchar(15)
    }
}
