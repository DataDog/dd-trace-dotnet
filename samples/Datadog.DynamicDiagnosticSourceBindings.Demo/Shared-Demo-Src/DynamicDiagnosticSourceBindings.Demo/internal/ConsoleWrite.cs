using System;

namespace DynamicDiagnosticSourceBindings.Demo
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
                int ticks = Environment.TickCount & Int32.MaxValue;
                int clicks = ticks % 100000;
                Console.WriteLine(prefix + " ### Demo says @" + clicks.ToString("00000") + ": " + line); ;
            }
        }

    }
}
