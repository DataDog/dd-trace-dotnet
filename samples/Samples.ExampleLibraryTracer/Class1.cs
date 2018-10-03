using System;

namespace Samples.ExampleLibraryTracer
{
    public class Class1
    {
        public int Add(int x, int y)
        {
            return 2 * (x + y);
        }

        public virtual int Multiply(int x, int y)
        {
            return 2 * (x * y);
        }



        /// <summary>
        /// called when a method enters
        /// </summary>
        /// <param name="arguments">the arguments passed to the method</param>
        /// <returns>data that will be passed to OnMethodExit</returns>
        public static object OnMethodEnter(object[] arguments)
        {
            return null;
        }

        /// <summary>
        /// called when a method exits
        /// </summary>
        /// <param name="enter">the object returned by OnMethodEnter</param>
        /// <param name="exception">a possible exception that was thrown during the execution of the method</param>
        /// <param name="result">the result of the execution of the method</param>
        public static void OnMethodExit(object enter, ref Exception exception, ref object result)
        {
        }
    }
}
