using System;
using System.Globalization;

namespace Samples.Wcf
{
    public static class LoggingHelper
    {
        public static void WriteLineWithDate(string message)
            => Console.WriteLine($"{DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff tt", CultureInfo.InvariantCulture)} {message}");
    }
}
