// <copyright file="ExceptionRedactor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Util;

namespace Datadog.Trace.Logging.Internal;

internal static class ExceptionRedactor
{
    internal const string StackFrameAt = "   at ";
    internal const string Redacted = "REDACTED";
    private const string InFileLineNum = "in {0}:line {1}";

    /// <summary>
    /// Redacts a stacktrace by replacing non-Datadog and non-BCL stack frames with REDACTED.
    /// Uses code based on the <a href="https://github.com/dotnet/runtime/blob/v7.0.2/src/libraries/System.Private.CoreLib/src/System/Diagnostics/StackTrace.cs" >.NET Core <c>StackTrace</c></a> code and the
    /// <a href="https://referencesource.microsoft.com/#mscorlib/system/diagnostics/stacktrace.cs" >.NET Framework <c>StackTrace</c></a>
    /// Records the _type_ of the Exception instead of the exception message
    /// </summary>
    /// <param name="exception">The exception to generate the redacted stack trace for</param>
    /// <returns>The redacted stack trace</returns>
    public static string Redact(Exception exception)
    {
        var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);

        // Using recursion to handle inner exceptions so that
        // it matches the behaviour of Exception.ToString():
        //
        // System.Exception: Outer most message
        //  ---> System.InvalidOperationException: Inner message
        //  ---> System.ArgumentException: Innermost message
        //    at Program.Inner2()
        //    --- End of inner exception stack trace --- // end of ArgumentException callstack
        //    at Program.Inner2()
        //    at Program.Inner()
        //    --- End of inner exception stack trace ---  // end of InvalidOperationException callstack
        //    at Program.Inner()
        //    at Program.Outer()
        AddException(sb, exception, isInnerException: false);

        return StringBuilderCache.GetStringAndRelease(sb);

        static void AddException(StringBuilder sb, Exception ex, bool isInnerException)
        {
            if (isInnerException)
            {
                sb.Append(" ---> ");
            }

            sb.Append(ex.GetType().FullName ?? "Unknown Exception");

            if (ex.InnerException is { } inner)
            {
                AddException(sb, inner, isInnerException: true);
            }
            else
            {
                sb.AppendLine();
            }

            var stackTrace = new StackTrace(ex);
            if (stackTrace.FrameCount > 0)
            {
                RedactStackTrace(sb, stackTrace);
            }

            if (isInnerException)
            {
                sb.AppendLine("   --- End of inner exception stack trace ---");
            }
        }
    }

    // internal for testing
    internal static void RedactStackTrace(StringBuilder sb, StackTrace stackTrace)
    {
        for (var i = 0; i < stackTrace.FrameCount; i++)
        {
            var frame = stackTrace.GetFrame(i);
            var methodInfo = frame?.GetMethod();
            if (frame is null || methodInfo is null)
            {
                continue;
            }

            if (ShouldRedactFrame(methodInfo))
            {
                sb.Append(StackFrameAt);
                sb.AppendLine(Redacted);
                continue;
            }

            AppendFrame(sb, frame);
        }
    }

    private static bool ShouldRedactFrame(MethodBase mb)
        => mb.DeclaringType switch
        {
            null => true, // "global" function
            // NOTE: Keep this in sync with InstrumentationDefinitions SourceGenerator implementation in Sources.BuildInstrumentedAssemblies.IsKnownAssemblyPrefix()
            { Assembly.FullName: { } name } => !(name.StartsWith("Datadog.", StringComparison.Ordinal)
                                              || name.StartsWith("mscorlib,", StringComparison.Ordinal) // note that this uses ',' not '.' as it's the full assembly name
                                              || name.StartsWith("Microsoft.", StringComparison.Ordinal)
                                              || name.StartsWith("System.", StringComparison.Ordinal)
                                              || name.StartsWith("Azure.", StringComparison.Ordinal)
                                              || name.StartsWith("AWSSDK.", StringComparison.Ordinal)
                                              || InstrumentationDefinitions.IsInstrumentedAssembly(name)),
            _ => true, // no assembly
        };

#if NETFRAMEWORK
    private static void AppendFrame(StringBuilder sb, StackFrame sf)
    {
        var displayFilenames = true;
        var mb = sf.GetMethod();
        if (mb != null)
        {
            // We want a newline at the end of every line except for the last
            sb.Append(StackFrameAt);

            var t = mb.DeclaringType;
             // if there is a type (non global method) print it
            if (t != null)
            {
                sb.Append(t.FullName!.Replace('+', '.'));
                sb.Append(".");
            }

            sb.Append(mb.Name);

            // deal with the generic portion of the method
            if (mb is MethodInfo && ((MethodInfo)mb).IsGenericMethod)
            {
                Type[] typars = ((MethodInfo)mb).GetGenericArguments();
                sb.Append("[");
                var k = 0;
                var fFirstTyParam = true;
                while (k < typars.Length)
                {
                    if (fFirstTyParam == false)
                    {
                        sb.Append(",");
                    }
                    else
                    {
                        fFirstTyParam = false;
                    }

                    sb.Append(typars[k].Name);
                    k++;
                }

                sb.Append("]");
            }

            // arguments printing
            sb.Append("(");
            ParameterInfo[] pi = mb.GetParameters();
            var fFirstParam = true;
            for (var j = 0; j < pi.Length; j++)
            {
                if (fFirstParam == false)
                {
                    sb.Append(", ");
                }
                else
                {
                    fFirstParam = false;
                }

                var typeName = "<UnknownType>";
                if (pi[j].ParameterType != null)
                {
                    typeName = pi[j].ParameterType.Name;
                }

                sb.Append(typeName + " " + pi[j].Name);
            }

            sb.Append(")");

            // source location printing
            if (displayFilenames && sf.GetILOffset() != -1)
            {
                // If we don't have a PDB or PDB-reading is disabled for the module,
                // then the file name will be null.
                string? fileName = null;

                // Getting the filename from a StackFrame is a privileged operation - we won't want
                // to disclose full path names to arbitrarily untrusted code.  Rather than just omit
                // this we could probably trim to just the filename so it's still mostly usefull.
                try
                {
                    fileName = sf.GetFileName();
                }
                catch (NotSupportedException)
                {
                    // Having a deprecated stack modifier on the callstack (such as Deny) will cause
                    // a NotSupportedException to be thrown.  Since we don't know if the app can
                    // access the file names, we'll conservatively hide them.
                    displayFilenames = false;
                }
                catch (SecurityException)
                {
                    // If the demand for displaying filenames fails, then it won't
                    // succeed later in the loop.  Avoid repeated exceptions by not trying again.
                    displayFilenames = false;
                }

                if (fileName != null)
                {
                    // tack on " in c:\tmp\MyFile.cs:line 5"
                    sb.Append(' ');
                    sb.AppendFormat(CultureInfo.InvariantCulture, InFileLineNum, fileName, sf.GetFileLineNumber());
                }
            }

            // Can't get IsLastFrameFromForeignExceptionStackTrace as it's internal unfortunately, so,
            // if (sf.GetIsLastFrameFromForeignExceptionStackTrace())
            // {
            //     sb.Append(Environment.NewLine);
            //     sb.Append(Environment.GetResourceString("Exception_EndStackTraceFromPreviousThrow"));
            // }
            sb.AppendLine();
        }
    }
#else
    private static void AppendFrame(StringBuilder sb, StackFrame sf)
    {
        // Passing a default string for "at" in case SR.UsingResourceKeys() is true
        // as this is a special case and we don't want to have "Word_At" on stack traces.
        // We also want to pass in a default for inFileLineNumber.
        var mb = sf.GetMethod()!;
        if (ShowInStackTrace(mb))
        {
            sb.Append(StackFrameAt);

            var isAsync = false;
            var declaringType = mb.DeclaringType;
            var methodName = mb.Name;
            var methodChanged = false;
            if (declaringType != null && declaringType.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
            {
                isAsync = typeof(IAsyncStateMachine).IsAssignableFrom(declaringType);
                if (isAsync || typeof(IEnumerator).IsAssignableFrom(declaringType))
                {
                    methodChanged = TryResolveStateMachineMethod(ref mb, out declaringType);
                }
            }

            // if there is a type (non global method) print it
            // ResolveStateMachineMethod may have set declaringType to null
            if (declaringType != null)
            {
                // Append t.FullName, replacing '+' with '.'
                var fullName = declaringType.FullName!;
                for (var i = 0; i < fullName.Length; i++)
                {
                    var ch = fullName[i];
                    sb.Append(ch == '+' ? '.' : ch);
                }

                sb.Append('.');
            }

            sb.Append(mb.Name);

            // deal with the generic portion of the method
            if (mb is MethodInfo mi && mi.IsGenericMethod)
            {
                Type[] typars = mi.GetGenericArguments();
                sb.Append('[');
                var k = 0;
                var fFirstTyParam = true;
                while (k < typars.Length)
                {
                    if (!fFirstTyParam)
                    {
                        sb.Append(',');
                    }
                    else
                    {
                        fFirstTyParam = false;
                    }

                    sb.Append(typars[k].Name);
                    k++;
                }

                sb.Append(']');
            }

            ParameterInfo[]? pi = null;
            try
            {
                pi = mb.GetParameters();
            }
            catch
            {
                // The parameter info cannot be loaded, so we don't
                // append the parameter list.
            }

            if (pi != null)
            {
                // arguments printing
                sb.Append('(');
                var fFirstParam = true;
                for (var j = 0; j < pi.Length; j++)
                {
                    if (!fFirstParam)
                    {
                        sb.Append(", ");
                    }
                    else
                    {
                        fFirstParam = false;
                    }

                    var typeName = "<UnknownType>";
                    if (pi[j].ParameterType != null)
                    {
                        typeName = pi[j].ParameterType.Name;
                    }

                    sb.Append(typeName);
                    var parameterName = pi[j].Name;
                    if (parameterName != null)
                    {
                        sb.Append(' ');
                        sb.Append(parameterName);
                    }
                }

                sb.Append(')');
            }

            if (methodChanged)
            {
                // Append original method name e.g. +MoveNext()
                sb.Append('+');
                sb.Append(methodName);
                sb.Append('(').Append(')');
            }

            // source location printing
            if (sf!.GetILOffset() != -1)
            {
                // If we don't have a PDB or PDB-reading is disabled for the module,
                // then the file name will be null.
                var fileName = sf.GetFileName();

                if (fileName != null)
                {
                    // tack on " in c:\tmp\MyFile.cs:line 5"
                    sb.Append(' ');
                    sb.AppendFormat(CultureInfo.InvariantCulture, InFileLineNum, fileName, sf.GetFileLineNumber());
                }

                // don't bother showing IL offsets
            }

            // Can't get IsLastFrameFromForeignExceptionStackTrace as it's internal unfortunately, so,
            // we won't get these error boundaries in the redacted logs
            // if (sf.IsLastFrameFromForeignExceptionStackTrace && !isAsync)
            // {
            //     sb.AppendLine();
            //     sb.Append("--- End of stack trace from previous location ---");
            // }

            sb.AppendLine();
        }
    }

    private static bool ShowInStackTrace(MethodBase mb)
    {
        if ((mb.MethodImplementationFlags & MethodImplAttributes.AggressiveInlining) != 0)
        {
            // Aggressive Inlines won't normally show in the StackTrace; however for Tier0 Jit and
            // cross-assembly AoT/R2R these inlines will be blocked until Tier1 Jit re-Jits
            // them when they will inline. We don't show them in the StackTrace to bring consistency
            // between this first-pass asm and fully optimized asm.
            return false;
        }

#if NET6_0_OR_GREATER
        try
        {
            if (mb.IsDefined(typeof(StackTraceHiddenAttribute), inherit: false))
            {
                // Don't show where StackTraceHidden is applied to the method.
                return false;
            }

            Type? declaringType = mb.DeclaringType;
            // Methods don't always have containing types, for example dynamic RefEmit generated methods.
            if (declaringType != null &&
                declaringType.IsDefined(typeof(StackTraceHiddenAttribute), inherit: false))
            {
                // Don't show where StackTraceHidden is applied to the containing Type of the method.
                return false;
            }
        }
        catch
        {
            // Getting the StackTraceHiddenAttribute has failed, behave as if it was not present.
            // One of the reasons can be that the method mb or its declaring type use attributes
            // defined in an assembly that is missing.
        }
#endif
        return true;
    }

    private static bool TryResolveStateMachineMethod(ref MethodBase method, out Type declaringType)
    {
        declaringType = method.DeclaringType!;

        Type? parentType = declaringType.DeclaringType;
        if (parentType == null)
        {
            return false;
        }

        static MethodInfo[]? GetDeclaredMethods(Type type) =>
            type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        MethodInfo[]? methods = GetDeclaredMethods(parentType);
        if (methods == null)
        {
            return false;
        }

        foreach (MethodInfo candidateMethod in methods)
        {
            StateMachineAttribute[]? attributes = (StateMachineAttribute[])Attribute.GetCustomAttributes(candidateMethod, typeof(StateMachineAttribute), inherit: false);
            if (attributes == null)
            {
                continue;
            }

            bool foundAttribute = false, foundIteratorAttribute = false;
            foreach (StateMachineAttribute asma in attributes)
            {
                if (asma.StateMachineType == declaringType)
                {
                    foundAttribute = true;
#if NETCOREAPP3_1_OR_GREATER
                    foundIteratorAttribute |= asma is IteratorStateMachineAttribute || asma is AsyncIteratorStateMachineAttribute;
#else
                    foundIteratorAttribute |= asma is IteratorStateMachineAttribute;
#endif
                }
            }

            if (foundAttribute)
            {
                // If this is an iterator (sync or async), mark the iterator as changed, so it gets the + annotation
                // of the original method. Non-iterator async state machines resolve directly to their builder methods
                // so aren't marked as changed.
                method = candidateMethod;
                declaringType = candidateMethod.DeclaringType!;
                return foundIteratorAttribute;
            }
        }

        return false;
    }
#endif
}
