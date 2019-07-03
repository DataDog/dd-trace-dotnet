using System;

namespace Samples.AspNetCoreMvc2.Attributes
{
    public class StackTraceHelper
    {
        public static string[] GetUsefulStack()
        {
            var stackTrace = Environment.StackTrace;
            string[] methods = stackTrace.Split(new[] { " at " }, StringSplitOptions.None);
            return methods;
        }
    }
}
