using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Common;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SqlInjection;

internal static class DDBBTestHelper
{
    public static List<string> GetCommands()
    {
        var commands = new List<string>
        {
            "CREATE TABLE Persons (Id varchar(50) NOT NULL,Name varchar(50) NOT NULL,Surname varchar(50) NULL,Married bit NULL,Gender char(10) NULL,DateOfBirth datetime NULL,Email varchar(50) NULL,FlattedAddress varchar(500) NULL,Mobile varchar(50) NULL,NIF varchar(50) NULL,Phone varchar(50) NULL,PostalCode varchar(50) NULL,Details varchar(5000) NULL,CityIniqueId varchar(50) NULL,CountryUniqueId varchar(50) NULL,ImagePath varchar(500) NULL)",
            "CREATE TABLE Books(Id varchar(50) NOT NULL,Title nvarchar(500) NOT NULL,Price numeric(18, 2) NULL,ISBN nvarchar(50) NULL,\tPages int NOT NULL,    PublicationDate datetime NULL,    Author nvarchar(500) NOT NULL,    Publisher nvarchar(50) NULL,\tPrologue nvarchar(4000) NULL,\tFormat int NULL)",
            @"INSERT INTO Persons (Id, Name, Surname, Married, Gender, DateOfBirth, Email, FlattedAddress, Mobile, NIF, Phone, PostalCode, Details, CityIniqueId, CountryUniqueId, ImagePath) VALUES ('D305C1EB-B72E-4340-B5BA-A19D0105A6C2', 'Michael', 'Smith', 0, 'Female    ', NULL, 'Michael.Smith@gmail.com', 'Mountain Avenue 55', '650214751', '50099554L', '918084525', '28341', 'Not Defined', NULL, NULL, '~/Images/Antonio.jpg')",
            @"INSERT INTO Persons(Id, Name, Surname, Married, Gender, DateOfBirth, Email, FlattedAddress, Mobile, NIF, Phone, PostalCode, Details, CityIniqueId, CountryUniqueId, ImagePath) VALUES('D305C1EB-B72E-4340-B5BA-A19D0105A6C3', 'Vicent', 'Monet', 0, 'Female    ', NULL, 'Jerome.Monet@gmail.com', 'Rua Paris 77', '650213223', '50088741K', '914652532', '28026', 'Not Defined', NULL, NULL, NULL)",
            @"INSERT INTO Persons(Id, Name, Surname, Married, Gender, DateOfBirth, Email, FlattedAddress, Mobile, NIF, Phone, PostalCode, Details, CityIniqueId, CountryUniqueId, ImagePath) VALUES('D305C1EB-B72E-4340-B5BA-A19D0105A6C4', 'james', 'hughes', 0, '0         ', NULL, 'james.hughes@gmail.com', '16th avenue', '621236651', '58966221F', '938256232', '30360', 'Not Defined', NULL, NULL, NULL)",
            @"INSERT INTO Persons(Id, Name, Surname, Married, Gender, DateOfBirth, Email, FlattedAddress, Mobile, NIF, Phone, PostalCode, Details, CityIniqueId, CountryUniqueId, ImagePath) VALUES('f46eeae9-8c15-401c-abaa-a49f9727a84c', 'Vladimir', 'Ivanov', 0, 'Female    ', NULL, 'Vladimir.Ivanov@gmail.com', 'Green Avenue 99', '657253124', '50042111', '918084562', '26451', 'Not Defined', NULL, NULL, NULL)",
            @"INSERT INTO Persons(Id, Name, Surname, Married, Gender, DateOfBirth, Email, FlattedAddress, Mobile, NIF, Phone, PostalCode, Details, CityIniqueId, CountryUniqueId, ImagePath) VALUES('D305C1EB-B72E-4340-B5BA-A19D0105A6C1', 'Name1', 'Surname1', 0, 'Female    ', NULL, 'name1.surname1@gmail.com', 'Address1', 'Mob1', 'N1', 'Phone1', 'Zip1', '<script language=''javascript'' type=''text/javascript''>alert(''Stored XSS attack'');</script>', NULL, NULL, NULL)",
            @"INSERT INTO Books (Id, Title, Price, ISBN, Pages, PublicationDate, Author, Publisher, Prologue, Format) VALUES ('026decfd-bba3-4aa8-85d4-1c71ffcfe8e9', 'CLR via C#', CAST(50.00 AS Decimal(18, 2)), '0735669954', 894, NULL, ' Jeffrey Richter', 'Microsoft Press', 'bla bla bla', 0)",
            @"INSERT INTO Books (Id, Title, Price, ISBN, Pages, PublicationDate, Author, Publisher, Prologue, Format) VALUES ('026decfd-bba3-4aa8-85d4-1c71eecfe8e9', 'Quixote', CAST(50.00 AS Decimal(18, 2)), '073343954', 334, NULL, ' Cervantes', 'AB Press', 'something', 0)"
        };

        return commands;
    }
}

