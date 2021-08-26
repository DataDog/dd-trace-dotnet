using System;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace Datadog.Util
{
    internal static class ExceptionExtensions
    {
        /// <summary>
        /// <c>ExceptionDispatchInfo.Capture(exception).Throw()</c> produces moreinformatoce stack traces when rethrowing exceptions.
        /// This method rethrows the specified exception using <c>ExceptionDispatchInfo</c>. It is typed to return the exception, to enable writing concise code like:
        /// <code>
        ///   try
        ///   {
        ///       // ...
        ///   }
        ///   catch (Exception ex)
        ///   {
        ///       throw ex.Rethrow();
        ///   }
        /// </code>
        /// The throwing actually happens inside of the <c>Rethrow<.c> method. However, this syntaxt allows the compiler to know that 
        /// an exception will occur at that line. This prevents incorrect code analysis warnings/end errors such as 'missing return value',
        /// 'missing initialization' and similar.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Exception Rethrow(this Exception exception)
        {
            if (exception == null)
            {
                return null;
            }

            ExceptionDispatchInfo.Capture(exception).Throw();
            return exception;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ExceptionDispatchInfo CaptureDispatchInfo(this Exception exception)
        {
            if (exception == null)
            {
                return null;
            }

            return ExceptionDispatchInfo.Capture(exception);
        }
    }
}
