using System;

namespace Datadog.InstrumentedAssemblyGenerator
{
    internal class Logger
    {
        private const ConsoleColor ErrorColor = ConsoleColor.Red;
        private const ConsoleColor WarnColor = ConsoleColor.Yellow;
        private const ConsoleColor InfoColor = ConsoleColor.White;
        private const ConsoleColor VerboseColor = ConsoleColor.Gray;
        private const ConsoleColor SuccessColor = ConsoleColor.DarkGreen;

        public static void Error(string error)
        {
            Log(error, ErrorColor);
        }

        public static void Error(Exception ex)
        {
            Log(ex.ToString(), ErrorColor);
        }

        public static void Warn(string warn)
        {
            Log(warn, WarnColor);
        }

        public static void Warn(Exception ex)
        {
            Log(ex.ToString(), WarnColor);
        }

        public static void Successful(string success)
        {
            Log(success, SuccessColor);
        }

        public static void Info(string info)
        {
            Log(info, InfoColor);
        }

        public static void Debug(string message)
        {
#if DEBUG
            Log(message, VerboseColor);
#endif
        }

        public static void Verbose(string message)
        {
#if VERBOS
            Log(message, VerboseColor);
#endif
        }

        private static void Log(string message, ConsoleColor color)
        {
            ConsoleColor originalColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ForegroundColor = originalColor;
        }
    }
}