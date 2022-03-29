using System;

namespace Website_AspNet.Models
{
    public class FibonacciData
    {
        public static readonly FibonacciData Nothing = new FibonacciData(0, 0, TimeSpan.Zero);

        public readonly int Input;
        public readonly long Result;
        public readonly TimeSpan Duration;

        public FibonacciData(int input, long result, TimeSpan duration)
        {
            Input = input;
            Result = result;
            Duration = duration;
        }
    }
}
