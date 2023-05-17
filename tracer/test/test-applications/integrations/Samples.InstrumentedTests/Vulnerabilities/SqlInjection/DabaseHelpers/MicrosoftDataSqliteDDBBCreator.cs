#if NETCOREAPP2_1_OR_GREATER
using Microsoft.Data.Sqlite;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

public static class MicrosoftDataSqliteDdbbCreator
{
    public static SqliteConnection Create()
    {
        var builder = new SqliteConnectionStringBuilder();
        builder.DataSource = ":memory:";
        var conn = builder.ConnectionString;

        if (string.IsNullOrEmpty(conn))
        {
            throw new System.Exception("Cannot create sqlite database.");
        }

        var dbConnection = new SqliteConnection(conn);
        dbConnection.Open();
        string personCommand = "CREATE TABLE Persons (Id NOT NULL,Name varchar(50) NOT NULL,Surname varchar(50) NULL,Married bit NULL,Gender char(10) NULL,DateOfBirth datetime NULL,Email varchar(50) NULL,FlattedAddress varchar(500) NULL,Mobile varchar(50) NULL,NIF varchar(50) NULL,Phone varchar(50) NULL,PostalCode varchar(50) NULL,Details varchar(5000) NULL,CityIniqueId varchar(50) NULL,CountryUniqueId varchar(50) NULL,ImagePath varchar(500) NULL)";
        string booksCommand = "CREATE TABLE Books(Id NOT NULL,Title nvarchar(500) NOT NULL,Price numeric(18, 2) NULL,ISBN nvarchar(50) NULL,	Pages int NOT NULL,    PublicationDate datetime NOT NULL,    Author nvarchar(500) NOT NULL,    Publisher nvarchar(50) NULL,	Prologue nvarchar(4000) NULL,	Format int NULL)";

        new SqliteCommand(booksCommand, dbConnection).ExecuteReader();
        new SqliteCommand(personCommand, dbConnection).ExecuteReader();
        new SqliteCommand(@"INSERT INTO Persons (Id, Name, Surname, Married, Gender, DateOfBirth, Email, FlattedAddress, Mobile, NIF, Phone, PostalCode, Details, CityIniqueId, CountryUniqueId, ImagePath) VALUES ('D305C1EB-B72E-4340-B5BA-A19D0105A6C2', 'brian', 'smith', 0, 'Female    ', CAST(0x00006A1000000000 AS DateTime), 'brian.smith@gmail.com', '14th avenue', '650214751', '50099554L', '918084525', '28341', 'Not Defined', NULL, NULL, '~/Images/Antonio.jpg')", dbConnection).ExecuteReader();
        new SqliteCommand(@"INSERT INTO Persons(Id, Name, Surname, Married, Gender, DateOfBirth, Email, FlattedAddress, Mobile, NIF, Phone, PostalCode, Details, CityIniqueId, CountryUniqueId, ImagePath) VALUES('D305C1EB-B72E-4340-B5BA-A19D0105A6C3', 'leo', 'stevens', 0, 'Female    ', CAST(0x00007DAD00000000 AS DateTime), 'leo.stevens@gmail.com', '15th avenue', '650213223', '50088741K', '914652532', '28026', 'Not Defined', NULL, NULL, NULL)", dbConnection).ExecuteReader();
        new SqliteCommand(@"INSERT INTO Persons(Id, Name, Surname, Married, Gender, DateOfBirth, Email, FlattedAddress, Mobile, NIF, Phone, PostalCode, Details, CityIniqueId, CountryUniqueId, ImagePath) VALUES('D305C1EB-B72E-4340-B5BA-A19D0105A6C4', 'james', 'hughes', 0, '0         ', CAST(0x00005DB000000000 AS DateTime), 'james.hughes@gmail.com', '16th avenue', '621236651', '58966221F', '938256232', '30360', 'Not Defined', NULL, NULL, NULL)", dbConnection).ExecuteReader();
        new SqliteCommand(@"INSERT INTO Persons(Id, Name, Surname, Married, Gender, DateOfBirth, Email, FlattedAddress, Mobile, NIF, Phone, PostalCode, Details, CityIniqueId, CountryUniqueId, ImagePath) VALUES('f46eeae9-8c15-401c-abaa-a49f9727a84c', 'betty', 'stevens', 0, 'Female    ', CAST(0x00006A1000000000 AS DateTime), 'betty.stevens@gmail.com', '17th avenue', '657253124', '50042111', '918084562', '26451', 'Not Defined', NULL, NULL, NULL)", dbConnection).ExecuteReader();
        new SqliteCommand(@"INSERT INTO Persons(Id, Name, Surname, Married, Gender, DateOfBirth, Email, FlattedAddress, Mobile, NIF, Phone, PostalCode, Details, CityIniqueId, CountryUniqueId, ImagePath) VALUES('D305C1EB-B72E-4340-B5BA-A19D0105A6C1', 'Name1', 'Surname1', 0, 'Female    ', CAST(0x00006A1000000000 AS DateTime), 'name1.surname1@gmail.com', 'Address1', 'Mob1', 'N1', 'Phone1', 'Zip1', '<script language=''javascript'' type=''text/javascript''>alert(''Stored XSS attack'');</script>', NULL, NULL, NULL)", dbConnection).ExecuteReader();
        new SqliteCommand(@"INSERT INTO Books (Id, Title, Price, ISBN, Pages, PublicationDate, Author, Publisher, Prologue, Format) VALUES ('026decfd-bba3-4aa8-85d4-1c71ffcfe8e9', 'CLR via C#', CAST(50.00 AS Decimal(18, 2)), '0735669954', 894, CAST(0x0000A03100000000 AS DateTime), ' Jeffrey Richter', 'Microsoft Press', 'bla bla bla', 0)", dbConnection).ExecuteReader();

        return (dbConnection);
    }
}

#endif
