using System;
using IBM.Data.DB2.iSeries;

namespace IBM.Data.DB2.DBCommand
{
    public class Program
    {
        public static void Main()
        {
            Console.WriteLine($"Profiler attached: {Samples.SampleHelpers.IsProfilerAttached()}");

            var command = new iDB2Command("MyCommand");
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


            Console.WriteLine("Done");
            Console.WriteLine("App completed successfully");
        }
    }
}
