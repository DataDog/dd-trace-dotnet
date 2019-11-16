using System;
using System.Data.Common;
using System.Data.SqlClient;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace AppDomain.Instance
{
    public class AppDomainInstanceProgram : MarshalByRefObject
    {
        public static void Main(string[] args)
        {
            new AppDomainInstanceProgram().Run(args);
        }

        public int Run(string[] args)
        {
            Console.WriteLine("Starting AppDomain Instance Test");

            string appDomainName = "crash-dummy";
            string programName = string.Empty;
            int index = 1;

            if (args?.Length > 0)
            {
                appDomainName = args[0];
                index = int.Parse(args[1]);
                programName = args[2];
            }

            NestedProgram instance;
            if (programName.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                instance = new SqlServerNestedProgram();
            }
            else if (programName.Equals("Elasticsearch", StringComparison.OrdinalIgnoreCase))
            {
                instance = new ElasticsearchNestedProgram();
            }
            else
            {
                Console.WriteLine($"programName {programName} not recognized. Exiting with error code -10.");
                return -10;
            }

            try
            {
                instance.AppDomainName = appDomainName;
                instance.AppDomainIndex = index;
                instance.Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"We have encountered an exception in this instance: {appDomainName} : {ex.Message}");
                Console.Error.WriteLine(ex);
                return -10;
            }

            Console.ReadKey();
            return 0;
        }
    }
}
