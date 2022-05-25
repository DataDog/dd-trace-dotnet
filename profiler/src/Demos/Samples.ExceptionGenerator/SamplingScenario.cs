using System;

namespace Samples.ExceptionGenerator
{
    internal class SamplingScenario
    {
        public void Run()
        {
            // First, throw 4000 exceptions
            new ParallelExceptionsScenario().Run();

            // Then, throw an exception of a type that wasn't seen before
            try
            {
                throw new InvalidOperationException("OK");
            }
            catch
            {
            }
        }
    }
}
