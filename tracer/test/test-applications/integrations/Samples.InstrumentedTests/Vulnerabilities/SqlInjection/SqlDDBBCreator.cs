using System;
using System.Data.SqlClient;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

internal class SqlDDBBCreator
{
    public static SqlConnection Create()
    {
        var connection = OpenConnection();

        string dropTablesCommand = "EXEC sp_MSforeachtable 'DROP TABLE ?'";
        new SqlCommand(dropTablesCommand, connection as SqlConnection).ExecuteNonQuery();

        string personCommand = "CREATE TABLE Persons (Id varchar(50) NOT NULL,Name varchar(50) NOT NULL,Surname varchar(50) NULL,Married bit NULL,Gender char(10) NULL,DateOfBirth datetime NULL,Email varchar(50) NULL,FlattedAddress varchar(500) NULL,Mobile varchar(50) NULL,NIF varchar(50) NULL,Phone varchar(50) NULL,PostalCode varchar(50) NULL,Details varchar(5000) NULL,CityIniqueId varchar(50) NULL,CountryUniqueId varchar(50) NULL,ImagePath varchar(500) NULL)";
        string booksCommand = "CREATE TABLE Books(Id varchar(50) NOT NULL,Title nvarchar(500) NOT NULL,Price numeric(18, 2) NULL,ISBN nvarchar(50) NULL,	Pages int NOT NULL,    PublicationDate datetime NOT NULL,    Author nvarchar(500) NOT NULL,    Editorial nvarchar(50) NULL,	Prologue nvarchar(4000) NULL,	Format int NULL)";

        new SqlCommand(booksCommand, connection as SqlConnection).ExecuteReader().Close();
        new SqlCommand(personCommand, connection as SqlConnection).ExecuteReader().Close();
        new SqlCommand(@"INSERT INTO Persons (Id, Name, Surname, Married, Gender, DateOfBirth, Email, FlattedAddress, Mobile, NIF, Phone, PostalCode, Details, CityIniqueId, CountryUniqueId, ImagePath) VALUES ('D305C1EB-B72E-4340-B5BA-A19D0105A6C2', 'Manuel', 'Martinez', 0, 'Female    ', CAST(0x00006A1000000000 AS DateTime), 'manuel.martinez@gmail.com', 'Avda. mar Caspio Nº 82 1ªG', '650214751', '50099554L', '918084525', '28341', 'Not Defined', NULL, NULL, '~/Images/Antonio.jpg')", connection as SqlConnection).ExecuteReader().Close();
        new SqlCommand(@"INSERT INTO Persons(Id, Name, Surname, Married, Gender, DateOfBirth, Email, FlattedAddress, Mobile, NIF, Phone, PostalCode, Details, CityIniqueId, CountryUniqueId, ImagePath) VALUES('D305C1EB-B72E-4340-B5BA-A19D0105A6C3', 'Leopoldo', 'Lopez', 0, 'Female    ', CAST(0x00007DAD00000000 AS DateTime), 'leopoldo.lopez@gmail.com', 'Paseo de la Castellana 174 2ºIaquierda', '650213223', '50088741K', '914652532', '28026', 'Not Defined', NULL, NULL, NULL)", connection as SqlConnection).ExecuteReader().Close();
        new SqlCommand(@"INSERT INTO Persons(Id, Name, Surname, Married, Gender, DateOfBirth, Email, FlattedAddress, Mobile, NIF, Phone, PostalCode, Details, CityIniqueId, CountryUniqueId, ImagePath) VALUES('D305C1EB-B72E-4340-B5BA-A19D0105A6C4', 'Santiago', 'Sanchez', 0, '0         ', CAST(0x00005DB000000000 AS DateTime), 'santiago.sanchez@gmail.com', 'Avda. de las cortes Nº4', '621236651', '58966221F', '938256232', '30360', 'Not Defined', NULL, NULL, NULL)", connection as SqlConnection).ExecuteReader().Close();
        new SqlCommand(@"INSERT INTO Persons(Id, Name, Surname, Married, Gender, DateOfBirth, Email, FlattedAddress, Mobile, NIF, Phone, PostalCode, Details, CityIniqueId, CountryUniqueId, ImagePath) VALUES('f46eeae9-8c15-401c-abaa-a49f9727a84c', 'Maria', 'Lopez', 0, 'Female    ', CAST(0x00006A1000000000 AS DateTime), 'maria.lopez@gmail.com', 'Avda. Aranjuez 24', '657253124', '50042111', '918084562', '26451', 'Not Defined', NULL, NULL, NULL)", connection as SqlConnection).ExecuteReader().Close();
        new SqlCommand(@"INSERT INTO Persons(Id, Name, Surname, Married, Gender, DateOfBirth, Email, FlattedAddress, Mobile, NIF, Phone, PostalCode, Details, CityIniqueId, CountryUniqueId, ImagePath) VALUES('D305C1EB-B72E-4340-B5BA-A19D0105A6C1', 'Name1', 'Surname1', 0, 'Female    ', CAST(0x00006A1000000000 AS DateTime), 'name1.surname1@gmail.com', 'Address1', 'Mob1', 'N1', 'Phone1', 'Zip1', '<script language=''javascript'' type=''text/javascript''>alert(''Stored XSS attack'');</script>', NULL, NULL, NULL)", connection as SqlConnection).ExecuteReader().Close();
        new SqlCommand(@"INSERT INTO Books (Id, Title, Price, ISBN, Pages, PublicationDate, Author, Editorial, Prologue, Format) VALUES ('026decfd-bba3-4aa8-85d4-1c71ffcfe8e9', 'CLR via C#', CAST(50.00 AS Decimal(18, 2)), '0735669954', 894, CAST(0x0000A03100000000 AS DateTime), ' Jeffrey Richter', 'Microsoft Press', 'bla bla bla', 0)", connection as SqlConnection).ExecuteReader().Close();
            
        return connection as SqlConnection;
    }

    private static SqlConnection OpenConnection()
    {
        int numAttempts = 3;
        var connectionString = @"Server=(localdb)\MSSQLLocalDB;Integrated Security=true;Connection Timeout=60";

        for (int i = 0; i < numAttempts; i++)
        {
            SqlConnection connection = null;

            try
            {
                connection = Activator.CreateInstance(typeof(SqlConnection), connectionString) as SqlConnection;
                connection.Open();
                return connection;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                connection?.Dispose();
            }
        }

        throw new Exception($"Unable to open connection to connection string {connectionString} after {numAttempts} attempts");
    }
}

