using System;
using System.Data.SQLite;
using System.Text;

namespace Samples.Security.Data
{
    public static class IastControllerHelper
    {
        public static SQLiteConnection CreateDatabase()
        {
            var builder = new SQLiteConnectionStringBuilder();
            builder.DataSource = ":memory:";
            var conn = builder.ConnectionString;

            if (string.IsNullOrEmpty(conn))
            {
                throw new System.Exception("Cannot create sqlite database.");
            }

            var dbConnection = new SQLiteConnection(conn);
            dbConnection.Open();
            string personCommand = "CREATE TABLE Persons (Id NOT NULL,Name varchar(50) NOT NULL,Surname varchar(50) NULL,Married bit NULL,Gender char(10) NULL,DateOfBirth datetime NULL,Email varchar(50) NULL,FlattedAddress varchar(500) NULL,Mobile varchar(50) NULL,NIF varchar(50) NULL,Phone varchar(50) NULL,PostalCode varchar(50) NULL,Details varchar(5000) NULL,CityIniqueId varchar(50) NULL,CountryUniqueId varchar(50) NULL,ImagePath varchar(500) NULL)";
            string booksCommand = "CREATE TABLE Books(Id NOT NULL,Title nvarchar(500) NOT NULL,Price numeric(18, 2) NULL,ISBN nvarchar(50) NULL,	Pages int NOT NULL,    PublicationDate datetime NOT NULL,    Author nvarchar(500) NOT NULL,    Editorial nvarchar(50) NULL,	Prologue nvarchar(4000) NULL,	Format int NULL)";

            new SQLiteCommand(booksCommand, dbConnection).ExecuteReader();
            new SQLiteCommand(personCommand, dbConnection).ExecuteReader();
            new SQLiteCommand(@"INSERT INTO Persons (Id, Name, Surname, Married, Gender, DateOfBirth, Email, FlattedAddress, Mobile, NIF, Phone, PostalCode, Details, CityIniqueId, CountryUniqueId, ImagePath) VALUES ('D305C1EB-B72E-4340-B5BA-A19D0105A6C2', 'Michael', 'Smith', 0, 'Female    ', CAST(0x00006A1000000000 AS DateTime), 'Michael.Smith@gmail.com', 'Mountain Avenue 55', '650214751', '50099554L', '918084525', '28341', 'Details', NULL, NULL, '~/Images/Antonio.jpg')", dbConnection).ExecuteReader();
            new SQLiteCommand(@"INSERT INTO Persons(Id, Name, Surname, Married, Gender, DateOfBirth, Email, FlattedAddress, Mobile, NIF, Phone, PostalCode, Details, CityIniqueId, CountryUniqueId, ImagePath) VALUES('D305C1EB-B72E-4340-B5BA-A19D0105A6C3', 'Vicent', 'Monet', 0, 'Female    ', CAST(0x00007DAD00000000 AS DateTime), 'Jerome.Monet@gmail.com', 'Rua Paris 77', '650213223', '50088741K', '914652532', '28026', 'Not Defined', NULL, NULL, NULL)", dbConnection).ExecuteReader();
            new SQLiteCommand(@"INSERT INTO Persons(Id, Name, Surname, Married, Gender, DateOfBirth, Email, FlattedAddress, Mobile, NIF, Phone, PostalCode, Details, CityIniqueId, CountryUniqueId, ImagePath) VALUES('D305C1EB-B72E-4340-B5BA-A19D0105A6C4', 'Santiago', 'Sanchez', 0, '0         ', CAST(0x00005DB000000000 AS DateTime), 'santiago.sanchez@gmail.com', 'Avda. de las cortes Nº4', '621236651', '58966221F', '938256232', '30360', 'Not Defined', NULL, NULL, NULL)", dbConnection).ExecuteReader();
            new SQLiteCommand(@"INSERT INTO Persons(Id, Name, Surname, Married, Gender, DateOfBirth, Email, FlattedAddress, Mobile, NIF, Phone, PostalCode, Details, CityIniqueId, CountryUniqueId, ImagePath) VALUES('f46eeae9-8c15-401c-abaa-a49f9727a84c', 'Vladimir', 'Ivanov', 0, 'Female    ', CAST(0x00006A1000000000 AS DateTime), 'Vladimir.Ivanov@gmail.com', 'Green Avenue 99', '657253124', '50042111', '918084562', '26451', 'Not Defined', NULL, NULL, NULL)", dbConnection).ExecuteReader();
            new SQLiteCommand(@"INSERT INTO Persons(Id, Name, Surname, Married, Gender, DateOfBirth, Email, FlattedAddress, Mobile, NIF, Phone, PostalCode, Details, CityIniqueId, CountryUniqueId, ImagePath) VALUES('D305C1EB-B72E-4340-B5BA-A19D0105A6C1', 'Name1', 'Surname1', 0, 'Female    ', CAST(0x00006A1000000000 AS DateTime), 'name1.surname1@gmail.com', 'Address1', 'Mob1', 'N1', 'Phone1', 'Zip1', '<script language=''javascript'' type=''text/javascript''>alert(''Stored XSS attack'');</script>', NULL, NULL, NULL)", dbConnection).ExecuteReader();
            new SQLiteCommand(@"INSERT INTO Books (Id, Title, Price, ISBN, Pages, PublicationDate, Author, Editorial, Prologue, Format) VALUES ('026decfd-bba3-4aa8-85d4-1c71ffcfe8e9', 'CLR via C#', CAST(50.00 AS Decimal(18, 2)), '0735669954', 894, CAST(0x0000A03100000000 AS DateTime), ' Jeffrey Richter', 'Microsoft Press', 'bla bla bla', 0)", dbConnection).ExecuteReader();

            return (dbConnection);
        }

        public static string ToFormattedString(Exception ex)
        {
            var message = new StringBuilder();
            if (ex != null)
            {
                message.AppendFormat("Name:       {0}", ex.GetType().FullName);
                message.Append(Environment.NewLine);
                message.AppendFormat("Message:    {0}", GetFullMessage(ex));
                message.Append(Environment.NewLine);
                message.AppendFormat("StackTrace: {0}", ex.StackTrace);
                message.Append(Environment.NewLine);
            }
            return message.ToString();
        }

        public static string GetFullMessage(Exception err)
        {
            string res = "";
            while (err != null)
            {
                res += err.Message + " ";
                err = err.InnerException;
            }
            return res;
        }
    }
}
