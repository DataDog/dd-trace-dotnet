using System;
using System.Data.SqlClient;
using System.Data.SQLite;
using System.Text;
#if NETCOREAPP
using Npgsql;
#endif

namespace Samples.Security.Data
{
    public static class IastControllerHelper
    {
        private static readonly string[] SqlServerTables =
            [
                "CREATE TABLE Persons (Id uniqueidentifier NOT NULL,Name varchar(50) NOT NULL,Surname varchar(50) NULL,Gender char(10) NULL,Email varchar(50) NULL,FlattedAddress varchar(500) NULL,Mobile varchar(50) NULL,NIF varchar(50) NULL,Phone varchar(50) NULL,PostalCode varchar(50) NULL,Details varchar(5000) NULL,CityIniqueId varchar(50) NULL,CountryUniqueId varchar(50) NULL,ImagePath varchar(500) NULL)",
                @"CREATE TABLE Books(Id uniqueidentifier NOT NULL,Title nvarchar(500) NOT NULL,Price numeric(18, 2) NULL,ISBN nvarchar(50) NULL,	Pages int NOT NULL,    Author nvarchar(500) NOT NULL,    Editorial nvarchar(50) NULL,	Prologue nvarchar(4000) NULL,	Format int NULL)",
            ];

        private static readonly string[] PostgresTables =
            [
                "DROP TABLE IF EXISTS Persons, Books;",
                "CREATE TABLE Persons (Id uuid NOT NULL,Name varchar(50) NOT NULL,Surname varchar(50) NULL,Gender char(10) NULL,Email varchar(50) NULL,FlattedAddress varchar(500) NULL,Mobile varchar(50) NULL,NIF varchar(50) NULL,Phone varchar(50) NULL,PostalCode varchar(50) NULL,Details varchar(5000) NULL,CityIniqueId varchar(50) NULL,CountryUniqueId varchar(50) NULL,ImagePath varchar(500) NULL)",
                @"CREATE TABLE Books(Id uuid NOT NULL,Title varchar(500) NOT NULL,Price numeric(18, 2) NULL,ISBN varchar(50) NULL,	Pages int NOT NULL,    Author varchar(500) NOT NULL,    Editorial varchar(50) NULL,	Prologue varchar(4000) NULL,	Format int NULL)",
            ];

        private static readonly string[] MySqlTables =
        [
            "DROP TABLE IF EXISTS Persons, Books;",
            "CREATE TABLE Persons (Id char(36) NOT NULL,Name varchar(50) NOT NULL,Surname varchar(50) NULL,Gender char(10) NULL,Email varchar(50) NULL,FlattedAddress varchar(500) NULL,Mobile varchar(50) NULL,NIF varchar(50) NULL,Phone varchar(50) NULL,PostalCode varchar(50) NULL,Details varchar(5000) NULL,CityIniqueId varchar(50) NULL,CountryUniqueId varchar(50) NULL,ImagePath varchar(500) NULL)",
            @"CREATE TABLE Books(Id char(36) NOT NULL,Title varchar(500) NOT NULL,Price numeric(18, 2) NULL,ISBN varchar(50) NULL,	Pages int NOT NULL,    Author varchar(500) NOT NULL,    Editorial varchar(50) NULL,	Prologue varchar(4000) NULL,	Format int NULL)",
        ];

        private static readonly string[] SqliteTables =
        [
            "CREATE TABLE Persons (Id NOT NULL,Name varchar(50) NOT NULL,Surname varchar(50) NULL,Gender char(10) NULL,Email varchar(50) NULL,FlattedAddress varchar(500) NULL,Mobile varchar(50) NULL,NIF varchar(50) NULL,Phone varchar(50) NULL,PostalCode varchar(50) NULL,Details varchar(5000) NULL,CityIniqueId varchar(50) NULL,CountryUniqueId varchar(50) NULL,ImagePath varchar(500) NULL)",
            @"CREATE TABLE Books(Id NOT NULL,Title nvarchar(500) NOT NULL,Price numeric(18, 2) NULL,ISBN nvarchar(50) NULL,	Pages int NOT NULL,    Author nvarchar(500) NOT NULL,    Editorial nvarchar(50) NULL,	Prologue nvarchar(4000) NULL,	Format int NULL)",
        ];

        private static readonly string[] PopulateCommands =
        [
            "INSERT INTO Persons(Id, Name, Surname, Gender, Email, FlattedAddress, Mobile, NIF, Phone, PostalCode, Details, CityIniqueId, CountryUniqueId, ImagePath) VALUES ('D305C1EB-B72E-4340-B5BA-A19D0105A6C2', 'Michael', 'Smith', 'Female    ', 'Michael.Smith@gmail.com', 'Mountain Avenue 55', '650214751', '50099554L', '918084525', '28341', 'Details', NULL, NULL, '~/Images/Antonio.jpg')",
            "INSERT INTO Persons(Id, Name, Surname, Gender, Email, FlattedAddress, Mobile, NIF, Phone, PostalCode, Details, CityIniqueId, CountryUniqueId, ImagePath) VALUES('D305C1EB-B72E-4340-B5BA-A19D0105A6C3', 'Vicent', 'Monet', 'Female    ', 'Jerome.Monet@gmail.com', 'Rua Paris 77', '650213223', '50088741K', '914652532', '28026', 'Not Defined', NULL, NULL, NULL)",
            "INSERT INTO Persons(Id, Name, Surname, Gender, Email, FlattedAddress, Mobile, NIF, Phone, PostalCode, Details, CityIniqueId, CountryUniqueId, ImagePath) VALUES('D305C1EB-B72E-4340-B5BA-A19D0105A6C4', 'Santiago', 'Sanchez', '0         ', 'santiago.sanchez@gmail.com', 'Avda. de las cortes Nº4', '621236651', '58966221F', '938256232', '30360', 'Not Defined', NULL, NULL, NULL)",
            "INSERT INTO Persons(Id, Name, Surname, Gender, Email, FlattedAddress, Mobile, NIF, Phone, PostalCode, Details, CityIniqueId, CountryUniqueId, ImagePath) VALUES('f46eeae9-8c15-401c-abaa-a49f9727a84c', 'Vladimir', 'Ivanov', 'Female    ', 'Vladimir.Ivanov@gmail.com', 'Green Avenue 99', '657253124', '50042111', '918084562', '26451', 'Not Defined', NULL, NULL, NULL)",
            "INSERT INTO Persons(Id, Name, Surname, Gender, Email, FlattedAddress, Mobile, NIF, Phone, PostalCode, Details, CityIniqueId, CountryUniqueId, ImagePath) VALUES('D305C1EB-B72E-4340-B5BA-A19D0105A6C1', 'Name1', 'Surname1', 'Female    ', 'name1.surname1@gmail.com', 'Address1', 'Mob1', 'N1', 'Phone1', 'Zip1', '<script language=''javascript'' type=''text/javascript''>alert(''Stored XSS attack'');</script>', NULL, NULL, NULL)",
            "INSERT INTO Books (Id, Title, Price, ISBN, Pages, Author, Editorial, Prologue, Format) VALUES ('026decfd-bba3-4aa8-85d4-1c71ffcfe8e9', 'CLR via C#', CAST(50.00 AS Decimal(18, 2)), '0735669954', 894, ' Jeffrey Richter', 'Microsoft Press', 'bla bla bla', 0)"
        ];

        public static MySqlConnection CreateMySqlDatabase()
        {
            var connectionString = Environment.GetEnvironmentVariable("MYSQL_CONNECTION_STRING");

            if (connectionString == null)
            {
                var host = Environment.GetEnvironmentVariable("MYSQL_HOST") ?? "localhost";
                var port = Environment.GetEnvironmentVariable("MYSQL_PORT") ?? "3307";
                connectionString = $"server={host};user=mysqldb;password=mysqldb;port={port};database=world";
            }

            var connection = new MySqlConnection(connectionString);
            connection.Open();

            foreach (var command in MySqlTables)
            {
                new MySqlCommand(command, connection).ExecuteNonQuery();
            }
            foreach (var command in PopulateCommands)
            {
                new MySqlCommand(command, connection).ExecuteNonQuery();
            }

            return connection;
        }

        public static NpgsqlConnection CreatePostgresDatabase()
        {
            // assumes the postgres docker image is running...
            var connection = new NpgsqlConnection("Host=localhost;Username=postgres;Password=postgres;Database=postgres;");
            connection.Open();

            foreach (var command in PostgresTables)
            {
                new NpgsqlCommand(command, connection).ExecuteNonQuery();
            }
            foreach (var command in PopulateCommands)
            {
                new NpgsqlCommand(command, connection).ExecuteNonQuery();
            }

            return connection;
        }

        public static SqlConnection CreateSqlServerDatabase()
        {
            // assumes the sqlserver docker image is running...
            using (var masterConnection = new SqlConnection("Server=localhost;Database=master;User Id=sa;Password=Strong!Passw0rd;"))
            {
                masterConnection.Open();
                new SqlCommand("IF DB_ID (N'books') IS NOT NULL\nDROP DATABASE books;", masterConnection).ExecuteNonQuery();
                new SqlCommand("CREATE DATABASE books;", masterConnection).ExecuteNonQuery();
            }

            var connection = new SqlConnection("Server=localhost;Database=books;User Id=sa;Password=Strong!Passw0rd;");
            connection.Open();

            foreach (var command in SqlServerTables)
            {
                new SqlCommand(command, connection).ExecuteNonQuery();
            }
            foreach (var command in PopulateCommands)
            {
                new SqlCommand(command, connection).ExecuteNonQuery();
            }

            return connection;
        }

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

            foreach (var command in SqliteTables)
            {
                new SQLiteCommand(command, dbConnectionSystemData).ExecuteReader();
            }
            foreach (var command in PopulateCommands)
            {
                new SQLiteCommand(command, dbConnectionSystemData).ExecuteReader();
            }

            return dbConnectionSystemData;
        }

#if NETCOREAPP
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
            
            foreach (var command in SqliteTables)
            {
                new SqliteCommand(command, dbConnectionMicrosoftData).ExecuteReader();
            }
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
