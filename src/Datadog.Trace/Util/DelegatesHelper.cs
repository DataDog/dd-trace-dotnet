using System;
using System.Diagnostics;
using System.Reflection;

namespace Datadog.Trace.Util
{
    /// <summary>
    /// Delegates helper class
    /// </summary>
    internal static class DelegatesHelper
    {
        /// <summary>
        /// Tries to get the internal ProcessExit delegate from the event using reflection.
        /// </summary>
        /// <returns>MulticastDelegate instance or null</returns>
        public static MulticastDelegate GetInternalProcessExitDelegate()
        {
            // This methods tries to get the internal delegte for the process exit event.

#if NETSTANDARD2_0 || NETCOREAPP
            // code for netcoreapp2.1, netcoreapp3.1, net5.0
            var appContextProcessExit = typeof(AppContext).GetField("ProcessExit", BindingFlags.NonPublic | BindingFlags.Static);
            if (appContextProcessExit != null)
            {
                return appContextProcessExit.GetValue(null) as MulticastDelegate;
            }
#endif
            // code for .NET Framework
            var appDomainProcessExit = typeof(AppDomain).GetField("_processExit", BindingFlags.NonPublic | BindingFlags.Instance);
            if (appDomainProcessExit != null)
            {
                return appDomainProcessExit.GetValue(AppDomain.CurrentDomain) as MulticastDelegate;
            }

            // code for Mono
            appDomainProcessExit = typeof(AppDomain).GetField("ProcessExit", BindingFlags.NonPublic | BindingFlags.Instance);
            if (appDomainProcessExit != null)
            {
                return appDomainProcessExit.GetValue(AppDomain.CurrentDomain) as MulticastDelegate;
            }

            return null;
        }

        /// <summary>
        /// Tries to get the internal Console.CancelKeyPress delegate from the event using reflection.
        /// </summary>
        /// <returns>MulticastDelegate instance or null</returns>
        public static MulticastDelegate GetInternalCancelKeyPressDelegate()
        {
            // This methods tries to get the internal delegte for the Console.CancelKeyPress event.

            FieldInfo consoleCancelCallbacks =
                typeof(Console).GetField("s_cancelCallbacks", BindingFlags.NonPublic | BindingFlags.Static) ??
                typeof(Console).GetField("_cancelCallbacks", BindingFlags.NonPublic | BindingFlags.Static);

            if (consoleCancelCallbacks != null)
            {
                return consoleCancelCallbacks.GetValue(null) as MulticastDelegate;
            }

            return null;
        }

        /// <summary>
        /// Try to set a delegate as the last one from a MulticastDelegate invocation list.
        /// </summary>
        /// <param name="targetDelegate">MulticastDelegate instance target were the invocation list will be modified</param>
        /// <param name="lastDelegate">Last delegate to run on the MulticastDelegate</param>
        /// <returns>true if the modification was made; otherwise, false.</returns>
        public static bool TrySetLastDelegate(MulticastDelegate targetDelegate, Delegate lastDelegate)
        {
            // The MulticastDelegate class has two inner fields with the invocation list and the count
            // (the invocation list is an array but the resize is handled similar to a list in a 2x factor)
            // Mono behaves different to .NET Framework and .NETCore, using a single `delegates` array.

            Type multicastType = typeof(MulticastDelegate);
            BindingFlags bindingFlags = BindingFlags.NonPublic | BindingFlags.Instance;
            FieldInfo invocationListField = multicastType.GetField("_invocationList", bindingFlags);
            FieldInfo invocationCountField = multicastType.GetField("_invocationCount", bindingFlags);

            if (invocationListField is null || invocationCountField is null)
            {
                // We try the mono approach
                FieldInfo delegatesField = multicastType.GetField("delegates", bindingFlags);
                if (delegatesField.GetValue(targetDelegate) is Delegate[] delegates && delegates.Length > 1)
                {
                    Delegate oldDelegate = delegates[delegates.Length - 1];
                    // Here we assume the caller of this method is the one is handling the event,
                    // so we check if we are not already the last delegate before trying to combine new delegates.
                    var frame = new StackFrame(1);
                    if (oldDelegate.Method != frame.GetMethod())
                    {
                        // Combine the last delegate in the invocation list with the one we want to be the last.
                        delegates[delegates.Length - 1] = Delegate.Combine(oldDelegate, lastDelegate);
                        return true;
                    }
                }

                return false;
            }

            if (invocationListField.GetValue(targetDelegate) is object[] invocationList)
            {
                if (invocationCountField.GetValue(targetDelegate) is IntPtr invocationCountPtr)
                {
                    // The internal count is saved as a IntPtr but contains an int value, so we cast.
                    int invocationCount = (int)invocationCountPtr;

                    if (invocationCount > 1 && invocationCount <= invocationList.Length)
                    {
                        Delegate oldDelegate = (Delegate)invocationList[invocationCount - 1];
                        // Here we assume the caller of this method is the one is handling the event,
                        // so we check if we are not already the last delegate before trying to combine new delegates.
                        StackFrame frame = new StackFrame(1);
                        if (oldDelegate.Method != frame.GetMethod())
                        {
                            // Combine the last delegate in the invocation list with the one we want to be the last.
                            invocationList[invocationCount - 1] = Delegate.Combine(oldDelegate, lastDelegate);
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
