using System;
using System.Threading;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using Samples.DatabaseHelper;

namespace Samples.OracleMDA
{
    internal static class Program
    {
        private static async Task Main()
        {
            var cts = new CancellationTokenSource();

            // allow time to flush
            await Task.Delay(2000, cts.Token);
        }

        private static OracleConnection OpenConnection()
        {
            return null;
        }
    }
}
