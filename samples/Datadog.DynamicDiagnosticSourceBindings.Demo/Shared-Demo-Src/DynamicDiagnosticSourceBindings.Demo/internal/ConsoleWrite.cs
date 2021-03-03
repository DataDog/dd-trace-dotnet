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
            if (line == null)
            {
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine(" ### Demo says: " + line);
            }
        }

        public static void LineLine(string line)
        {
            if (line == null)
            {
                Console.WriteLine(Environment.NewLine);
            }
            else
            {
                Console.WriteLine(Environment.NewLine + " ### Demo says: " + line);
            }
        }

        internal static void LineLine(object p)
        {
            throw new NotImplementedException();
        }
    }
}
