using System;
using System.Threading.Tasks;

namespace Samples.Wcf.Server
{
    public class CalculatorService : ICalculator
    {
        public double ServerSyncAdd(double n1, double n2)
        {
            Console.WriteLine("[Server] Received ServerSyncAdd({0},{1})", n1, n2);
            double result = n1 + n2;

            Console.WriteLine("[Server] Return: {0}", result);
            return result;
        }

        public async Task<double> ServerTaskAdd(double n1, double n2)
        {
            Console.WriteLine("[Server] Received ServerTaskAdd({0},{1})", n1, n2);
            double result = await PerformAddWithDelay(n1, n2);
            Console.WriteLine("[Server] Return: {0}", result);
            return result;
        }

        public IAsyncResult BeginServerAsyncAdd(double n1, double n2, AsyncCallback callback, object state)
        {
            Console.WriteLine("[Server] Received BeginServerAsyncAdd({0},{1})", n1, n2);
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
            Console.WriteLine("[Server] Received EndServerAsyncAdd(asyncResult)");
            Console.WriteLine("[Server] Return: {0}", asyncResult);
            return ((Task<double>)asyncResult).Result;
        }

        private async Task<double> PerformAddWithDelay(double n1, double n2)
        {
            await Task.Delay(50);
            return n1 + n2;
        }
    }
}
