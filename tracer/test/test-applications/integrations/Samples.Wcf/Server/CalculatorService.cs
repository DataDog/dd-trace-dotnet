using System;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.Wcf.Server
{
    public class CalculatorService : ICalculator
    {
        public double ServerSyncAdd(double n1, double n2)
        {
            LoggingHelper.WriteLineWithDate($"[Server] Received ServerSyncAdd({n1},{n2})");

            Thread.Sleep(1);

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

        public IAsyncResult BeginServerAsyncAdd(double n1, double n2, bool throwsException, bool synchronouslyCompletes, AsyncCallback callback, object state)
        {
            LoggingHelper.WriteLineWithDate($"[Server] Received BeginServerAsyncAdd({n1},{n2},{throwsException},{synchronouslyCompletes})");

            Thread.Sleep(1);

            var asyncResult = new SimpleAsyncResult<Tuple<double, bool>>(state);

            if (synchronouslyCompletes)
            {
                if (throwsException)
                {
                    throw new FaultException("Something happened");
                }

                asyncResult.CompleteSynchronously(Tuple.Create(n1 + n2, false));
                callback(asyncResult);
            }
            else
            {
                var task = PerformAddWithDelay(n1, n2);
                task.ContinueWith(
                    t =>
                    {
                        asyncResult.Complete(Tuple.Create(t.Result, throwsException));
                        callback(asyncResult);
                    });
            }

            return asyncResult;
        }

        public double EndServerAsyncAdd(IAsyncResult asyncResult)
        {
            LoggingHelper.WriteLineWithDate("[Server] Received EndServerAsyncAdd(asyncResult)");
            LoggingHelper.WriteLineWithDate($"[Server] Return: {asyncResult}");

            Thread.Sleep(1);

            var result = (SimpleAsyncResult<Tuple<double, bool>>)asyncResult;

            if (result.Result.Item2)
            {
                throw new FaultException("Something happened");
            }

            return result.Result.Item1;
        }

        private async Task<double> PerformAddWithDelay(double n1, double n2)
        {
            await Task.Delay(1);
            return n1 + n2;
        }

        public double ServerEmptyActionAdd(double n1, double n2)
        {
            LoggingHelper.WriteLineWithDate($"[Server] Received ServerEmptyActionAdd({n1}, {n2})");
            double result = n1 + n2;

            Thread.Sleep(1);

            LoggingHelper.WriteLineWithDate($"[Server] Return: {result}");
            return result;
        }

        private class SimpleAsyncResult<T> : IAsyncResult
        {
            private readonly ManualResetEvent _mutex = new ManualResetEvent(false);

            public SimpleAsyncResult(object state)
            {
                AsyncState = state;
            }

            public void Complete(T result)
            {
                Result = result;
                IsCompleted = true;
                _mutex.Set();
            }

            public void CompleteSynchronously(T result)
            {
                CompletedSynchronously = true;
                Complete(result);
            }

            public T Result { get; private set; }

            public bool IsCompleted { get; private set; }
            public WaitHandle AsyncWaitHandle => _mutex;
            public object AsyncState { get; }
            public bool CompletedSynchronously { get; private set; }
        }

    }
}
