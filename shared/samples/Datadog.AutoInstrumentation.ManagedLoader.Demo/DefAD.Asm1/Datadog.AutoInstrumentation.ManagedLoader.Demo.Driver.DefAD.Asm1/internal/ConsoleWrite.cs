using System;

namespace Datadog.AutoInstrumentation.ManagedLoader.Demo.Driver.DefAD.Asm1
{
    internal static class ConsoleWrite
    {
        public static void Line()
        {
            Line(null);
        }

        public static void LineLine()
        {
            Line(null);
        }

        public static void Exception(Exception exception)
        {
            LineLine(exception?.ToString());
        }

        public static void Line(string line)
        {
            PrintLine(String.Empty, line);
        }

        public static void LineLine(string line)
        {
            PrintLine(Environment.NewLine, line);
        }

        private static void PrintLine(string prefix, string line)
        {
            if (line == null)
            {
                Console.WriteLine(prefix);
            }
            else
            {
                const string TimestampPattern = @"HH\:mm\:ss\.ffff";

                Console.WriteLine(prefix + " ### Demo says (@" + DateTimeOffset.Now.ToString(TimestampPattern) + "): " + line);
            }
        }

    }
}
