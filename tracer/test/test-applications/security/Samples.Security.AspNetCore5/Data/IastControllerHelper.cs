using System;
using System.Data.SQLite;
using System.Text;
#if !NETFRAMEWORK
using Microsoft.Data.Sqlite;
#endif

namespace Samples.Security.Data
{
    public static class IastControllerHelper
    {
        private static readonly string[] PopulateCommands =
        [
            "CREATE TABLE Persons (Id NOT NULL,Name varchar(50) NOT NULL,Surname varchar(50) NULL,Married bit NULL,Gender char(10) NULL,DateOfBirth datetime NULL,Email varchar(50) NULL,FlattedAddress varchar(500) NULL,Mobile varchar(50) NULL,NIF varchar(50) NULL,Phone varchar(50) NULL,PostalCode varchar(50) NULL,Details varchar(5000) NULL,CityIniqueId varchar(50) NULL,CountryUniqueId varchar(50) NULL,ImagePath varchar(500) NULL)",
            @"CREATE TABLE Books(Id NOT NULL,Title nvarchar(500) NOT NULL,Price numeric(18, 2) NULL,ISBN nvarchar(50) NULL,	Pages int NOT NULL,    PublicationDate datetime NOT NULL,    Author nvarchar(500) NOT NULL,    Editorial nvarchar(50) NULL,	Prologue nvarchar(4000) NULL,	Format int NULL)",
            "INSERT INTO Persons (Id, Name, Surname, Married, Gender, DateOfBirth, Email, FlattedAddress, Mobile, NIF, Phone, PostalCode, Details, CityIniqueId, CountryUniqueId, ImagePath) VALUES ('D305C1EB-B72E-4340-B5BA-A19D0105A6C2', 'Michael', 'Smith', 0, 'Female    ', CAST(0x00006A1000000000 AS DateTime), 'Michael.Smith@gmail.com', 'Mountain Avenue 55', '650214751', '50099554L', '918084525', '28341', 'Details', NULL, NULL, '~/Images/Antonio.jpg')",
            "INSERT INTO Persons(Id, Name, Surname, Married, Gender, DateOfBirth, Email, FlattedAddress, Mobile, NIF, Phone, PostalCode, Details, CityIniqueId, CountryUniqueId, ImagePath) VALUES('D305C1EB-B72E-4340-B5BA-A19D0105A6C3', 'Vicent', 'Monet', 0, 'Female    ', CAST(0x00007DAD00000000 AS DateTime), 'Jerome.Monet@gmail.com', 'Rua Paris 77', '650213223', '50088741K', '914652532', '28026', 'Not Defined', NULL, NULL, NULL)",
            "INSERT INTO Persons(Id, Name, Surname, Married, Gender, DateOfBirth, Email, FlattedAddress, Mobile, NIF, Phone, PostalCode, Details, CityIniqueId, CountryUniqueId, ImagePath) VALUES('D305C1EB-B72E-4340-B5BA-A19D0105A6C4', 'Santiago', 'Sanchez', 0, '0         ', CAST(0x00005DB000000000 AS DateTime), 'santiago.sanchez@gmail.com', 'Avda. de las cortes Nº4', '621236651', '58966221F', '938256232', '30360', 'Not Defined', NULL, NULL, NULL)",
            "INSERT INTO Persons(Id, Name, Surname, Married, Gender, DateOfBirth, Email, FlattedAddress, Mobile, NIF, Phone, PostalCode, Details, CityIniqueId, CountryUniqueId, ImagePath) VALUES('f46eeae9-8c15-401c-abaa-a49f9727a84c', 'Vladimir', 'Ivanov', 0, 'Female    ', CAST(0x00006A1000000000 AS DateTime), 'Vladimir.Ivanov@gmail.com', 'Green Avenue 99', '657253124', '50042111', '918084562', '26451', 'Not Defined', NULL, NULL, NULL)",
            "INSERT INTO Persons(Id, Name, Surname, Married, Gender, DateOfBirth, Email, FlattedAddress, Mobile, NIF, Phone, PostalCode, Details, CityIniqueId, CountryUniqueId, ImagePath) VALUES('D305C1EB-B72E-4340-B5BA-A19D0105A6C1', 'Name1', 'Surname1', 0, 'Female    ', CAST(0x00006A1000000000 AS DateTime), 'name1.surname1@gmail.com', 'Address1', 'Mob1', 'N1', 'Phone1', 'Zip1', '<script language=''javascript'' type=''text/javascript''>alert(''Stored XSS attack'');</script>', NULL, NULL, NULL)",
            "INSERT INTO Books (Id, Title, Price, ISBN, Pages, PublicationDate, Author, Editorial, Prologue, Format) VALUES ('026decfd-bba3-4aa8-85d4-1c71ffcfe8e9', 'CLR via C#', CAST(50.00 AS Decimal(18, 2)), '0735669954', 894, CAST(0x0000A03100000000 AS DateTime), ' Jeffrey Richter', 'Microsoft Press', 'bla bla bla', 0)"
        ];

        public static SQLiteConnection CreateSystemDataDatabase()
        {
            var builderSystemData = new SQLiteConnectionStringBuilder { DataSource = ":memory:" };
            var connSystemData = builderSystemData.ConnectionString;
            if (string.IsNullOrEmpty(connSystemData))
            {
                throw new Exception("Cannot create sqlite database using System.Data.SQLite.");
            }
            
            var dbConnectionSystemData = new SQLiteConnection(connSystemData);
            dbConnectionSystemData.Open();

            foreach (var command in PopulateCommands)
            {
                new SQLiteCommand(command, dbConnectionSystemData).ExecuteReader();
            }

            return dbConnectionSystemData;
        }
        
        #if !NETFRAMEWORK
        public static SqliteConnection CreateMicrosoftDataDatabase()
        {
            var builderMicrosoftData = new SqliteConnectionStringBuilder { DataSource = ":memory:" };
            var connMicrosoftData = builderMicrosoftData.ConnectionString;
            if (string.IsNullOrEmpty(connMicrosoftData))
            {
                throw new Exception("Cannot create sqlite database using Microsoft.Data.Sqlite.");
            }
            
            var dbConnectionMicrosoftData = new SqliteConnection(connMicrosoftData);
            dbConnectionMicrosoftData.Open();
            
            foreach (var command in PopulateCommands)
            {
                new SqliteCommand(command, dbConnectionMicrosoftData).ExecuteReader();
            }
            
            return dbConnectionMicrosoftData;
        }
        #endif

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
