using System;

namespace Devart.Data.DBCommand
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var command = new Devart.Data.PostgreSql.PgSqlCommand();
            try
            {
                command.ExecuteNonQuery();
            }
            catch (Exception)
            {
            }

            try
            {
                command.ExecuteScalar();
            }
            catch (Exception)
            {
            }


            try
            {
                command.ExecuteReader();
            }
            catch (Exception)
            {
            }
            Console.WriteLine("App completed successfully");
        }
    }
}
