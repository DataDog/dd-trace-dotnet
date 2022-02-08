using System.Diagnostics;

namespace Website_AspNet.Models
{
    public static class Fibonacci
    {
        public static FibonacciData Compute(int? number)
        {
            if (number == null || number.Value < 0)
            {
                return FibonacciData.Nothing;
            }

            if (number.Value > 50)
            {
                number = 50; // for now just to avoid mistake and breaking the app
            }

            var sw = Stopwatch.StartNew();
            var result = Compute(number.Value);
            var duration = sw.Elapsed;
            return new FibonacciData(number.Value, result, duration);
        }

        private static long Compute(int number)
        {
            if (number == 0)
            {
                return 0;
            }

            if (number  == 1)
            {
                return 1;
            }

            return Compute(number - 1) + Compute(number - 2);
        }
    }
}
