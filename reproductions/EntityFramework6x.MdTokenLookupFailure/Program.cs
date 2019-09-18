using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity.Core;
using System.Data.Entity.Core.EntityClient;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityFramework6x.MdTokenLookupFailure
{
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                using (var ctx = new SchoolDbContextEntities())
                {
                    // create database if missing
                    ctx.Database.CreateIfNotExists();

                    var student = new Student() { StudentName = "Bill", Age = 12 };

                    ctx.Students.Add(student);
                    ctx.SaveChanges();
                }

                // Specify the provider name, server and database.
                string providerName = "System.Data.SqlClient";
                string serverName = @"(localdb)\MSSQLLocalDB";
                string databaseName = "SchoolDbContext";

                // Initialize the connection string builder for the
                // underlying provider.
                SqlConnectionStringBuilder sqlBuilder =
                    new SqlConnectionStringBuilder();

                // Set the properties for the data source.
                sqlBuilder.DataSource = serverName;
                sqlBuilder.InitialCatalog = databaseName;
                sqlBuilder.IntegratedSecurity = true;

                // Build the SqlConnection connection string.
                string providerString = sqlBuilder.ToString();

                // Initialize the EntityConnectionStringBuilder.
                EntityConnectionStringBuilder entityBuilder =
                    new EntityConnectionStringBuilder();

                //Set the provider name.
                entityBuilder.Provider = providerName;

                // Set the provider-specific connection string.
                entityBuilder.ProviderConnectionString = providerString;

                // Set the Metadata location.
                entityBuilder.Metadata = @"res://*/SchoolModel.csdl|
                            res://*/SchoolModel.ssdl|
                            res://*/SchoolModel.msl";
                Console.WriteLine(entityBuilder.ToString());

                using (EntityConnection conn =
                    new EntityConnection(entityBuilder.ToString()))
                {
                    conn.Open();

                    using (EntityCommand cmd = conn.CreateCommand())
                    {
                        Console.WriteLine("Creating an EntityCommand with this EntityConnection.");
                        cmd.CommandText = "SELECT VALUE AVG(s.Age) FROM SchoolDbContextEntities.Students as s";
                        // Execute the command.
                        using (EntityDataReader rdr =
                            cmd.ExecuteReader(CommandBehavior.SequentialAccess))
                        {
                            // Start reading results.
                            while (rdr.Read())
                            {
                                IExtendedDataRecord record = rdr as IExtendedDataRecord;
                                // For PrimitiveType 
                                // the record contains exactly one field.
                                int fieldIndex = 0;
                                Console.WriteLine("Value: " + record.GetValue(fieldIndex));
                            }
                        }

                        conn.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return (int)ExitCode.UnknownError;
            }

            return (int)ExitCode.Success;
        }
    }

    enum ExitCode : int
    {
        Success = 0,
        UnknownError = -10
    }
}
