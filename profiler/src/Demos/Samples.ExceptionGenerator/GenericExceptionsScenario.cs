using System;
using System.Runtime.Serialization;
using System.Threading;

namespace Samples.ExceptionGenerator
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "No need in tests")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1649:File name should match first type name", Justification = "No need in tests")]
    public class GenericException<T> : Exception
    {
        private int _id;  // just to avoid using the message constructor
        private T _field;

        public GenericException(int id, T info)
            : this(typeof(T).ToString())
        {
            _id = id;
            _field = info;
        }

        public GenericException()
        {
        }

        public GenericException(string message)
            : base(message)
        {
        }

        public GenericException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected GenericException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }

    internal class GenericExceptionsScenario
    {
        private const int NumberOfThreads = 4;
        private const int ExceptionsPerThread = 1000;

        public void Run()
        {
            var barrier = new Barrier(NumberOfThreads);

            // Use threads instead of tasks and suppress execution context to have a predictable stacktrace
            var threads = new Thread[NumberOfThreads];

            using (ExecutionContext.SuppressFlow())
            {
                for (int i = 0; i < threads.Length; i++)
                {
                    threads[i] = new Thread(ThrowExceptions);
                    threads[i].Start(barrier);
                }
            }

            foreach (var thread in threads)
            {
                thread.Join();
            }
        }

        private void ThrowExceptions(object state)
        {
            var barrier = (Barrier)state;
            barrier.SignalAndWait();

            for (int i = 0; i < ExceptionsPerThread; i++)
            {
                // reference type
                try
                {
                    throw new GenericException<string>(1, "string");
                }
                catch
                {
                    // ignored
                }

                // value type
                try
                {
                    throw new GenericException<int>(2, 42);
                }
                catch
                {
                    // ignored
                }
            }
        }
    }
}
