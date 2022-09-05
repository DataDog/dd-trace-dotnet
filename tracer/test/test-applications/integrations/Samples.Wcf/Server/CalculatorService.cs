using System;
using System.Threading.Tasks;

namespace Samples.Wcf.Server
{
    public class CalculatorService : ICalculator
    {
        public double ServerSyncAdd(double n1, double n2)
        {
            LoggingHelper.WriteLineWithDate($"[Server] Received ServerSyncAdd({n1},{n2})");
            double result = n1 + n2;

            LoggingHelper.WriteLineWithDate($"[Server] Return: {result}");
            return result;
        }

        public async Task<double> ServerTaskAdd(double n1, double n2)
        {
            LoggingHelper.WriteLineWithDate($"[Server] Received ServerTaskAdd({n1},{n2})");
            double result = await PerformAddWithDelay(n1, n2);
            LoggingHelper.WriteLineWithDate($"[Server] Return: {result}");
            return result;
        }

        public IAsyncResult BeginServerAsyncAdd(double n1, double n2, AsyncCallback callback, object state)
        {
            LoggingHelper.WriteLineWithDate($"[Server] Received BeginServerAsyncAdd({n1},{n2})");
            var tcs = new TaskCompletionSource<double>(state);

            var task = PerformAddWithDelay(n1, n2);
            task.ContinueWith(t =>
            {
                tcs.SetResult(t.Result);
                callback(tcs.Task);
            });

            return tcs.Task;
        }

        public double EndServerAsyncAdd(IAsyncResult asyncResult)
        {
            LoggingHelper.WriteLineWithDate("[Server] Received EndServerAsyncAdd(asyncResult)");
            LoggingHelper.WriteLineWithDate($"[Server] Return: {asyncResult}");
            return ((Task<double>)asyncResult).Result;
        }

        private async Task<double> PerformAddWithDelay(double n1, double n2)
        {
            await Task.Delay(50);
            return n1 + n2;
        }

        public double ServerEmptyActionAdd(double n1, double n2)
        {
            LoggingHelper.WriteLineWithDate($"[Server] Received ServerEmptyActionAdd({n1}, {n2})");
            double result = n1 + n2;

            LoggingHelper.WriteLineWithDate($"[Server] Return: {result}");
            return result;
        }
    }
}
