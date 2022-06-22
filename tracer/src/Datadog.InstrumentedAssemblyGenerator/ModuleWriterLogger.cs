using System;
using System.Linq;
using System.Text;
using dnlib.DotNet;

namespace Datadog.InstrumentedAssemblyGenerator
{
    internal sealed class ModuleWriterLogger : ILogger
    {
        public StringBuilder ErrorsBuilder { get; } = new StringBuilder();

        /// <summary>
        /// When dnlib write the module, we want to be notified on errors and trying to understand which type of error is it.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="loggerEvent"></param>
        /// <param name="format"></param>
        /// <param name="args"></param>
		public void Log(object sender, LoggerEvent loggerEvent, string format, params object[] args)
        {
            switch (loggerEvent)
            {
                case LoggerEvent.Error:
                    string message = string.Format(format, args);
                    if (message == "Instruction operand is null")
                    {
                        message += ". Call Stack is:" + Environment.NewLine;
                        try
                        {
                            message += string.Join(Environment.NewLine, new System.Diagnostics.StackTrace(1).GetFrames()?.Take(4).Select(f => "\t" + (f.GetMethod()?.Name ?? "Unknown")) ?? "Unknown".Enumerate());
                            // TODO: this should be an error but I set it to debug till I be able to print here the method name, otherwise this error not so helpful in CI
                            Logger.Debug(message);
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                    else if (message.Contains("Error calculating max stack value."))
                    {
                        ErrorsBuilder.AppendLine(message.Substring(0, message.IndexOf("Error calculating", StringComparison.OrdinalIgnoreCase) - 2));
                    }

                    break;
                case LoggerEvent.Warning:
                    Logger.Warn(string.Format(format, args));
                    break;
                case LoggerEvent.Info:
                    Logger.Verbose(string.Format(format, args));
                    break;
            }
        }

        public bool IgnoresEvent(LoggerEvent loggerEvent)
        {
            return loggerEvent > LoggerEvent.Warning;
        }
    }
}
