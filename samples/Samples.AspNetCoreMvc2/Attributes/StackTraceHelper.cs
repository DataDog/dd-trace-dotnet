using System;
using System.Linq;

namespace Samples.AspNetCoreMvc2.Attributes
{
    public class StackTraceHelper
    {
        public static string[] GetUsefulStack()
        {
            var skip = 2;
            var stackTrace = Environment.StackTrace;
            string[] methods = stackTrace.Split(new[] { " at " }, StringSplitOptions.None);
            methods = methods.Skip(skip).Take(methods.Length - skip).ToArray();
            return methods;
        }
    }
}
