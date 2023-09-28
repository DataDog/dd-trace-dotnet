// Decompiled with JetBrains decompiler
// Type: System.Collections.Immutable.Requires
// Assembly: System.Collections.Immutable, Version=7.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 5F9FF90F-0D16-4469-A104-76829D3705E2
// Assembly location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.dll
// XML documentation location: C:\Users\dudi.keleti\.nuget\packages\system.collections.immutable\7.0.0\lib\net462\System.Collections.Immutable.xml

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;


#nullable enable
namespace Datadog.System.Collections.Immutable
{
    internal static class Requires
  {
    [DebuggerStepThrough]
    public static void NotNull<T>([ValidatedNotNull] T value, string? parameterName) where T : class
    {
      if ((object) value != null)
        return;
      Requires.FailArgumentNullException(parameterName);
    }

    [DebuggerStepThrough]
    public static T NotNullPassthrough<T>([ValidatedNotNull] T value, string? parameterName) where T : class
    {
      Requires.NotNull<T>(value, parameterName);
      return value;
    }

    [DebuggerStepThrough]
    public static void NotNullAllowStructs<T>([ValidatedNotNull] T value, string? parameterName)
    {
      if ((object) value != null)
        return;
      Requires.FailArgumentNullException(parameterName);
    }


    #nullable disable
    [DebuggerStepThrough]
    private static void FailArgumentNullException(string parameterName) => throw new ArgumentNullException(parameterName);


    #nullable enable
    [DebuggerStepThrough]
    public static void Range(bool condition, string? parameterName, string? message = null)
    {
      if (condition)
        return;
      Requires.FailRange(parameterName, message);
    }

    [DebuggerStepThrough]
    public static void FailRange(string? parameterName, string? message = null)
    {
      if (string.IsNullOrEmpty(message))
        throw new ArgumentOutOfRangeException(parameterName);
      throw new ArgumentOutOfRangeException(parameterName, message);
    }

    [DebuggerStepThrough]
    public static void Argument(bool condition, string? parameterName, string? message)
    {
      if (!condition)
        throw new ArgumentException(message, parameterName);
    }

    [DebuggerStepThrough]
    public static void Argument(bool condition)
    {
      if (!condition)
        throw new ArgumentException();
    }

    [DebuggerStepThrough]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void FailObjectDisposed<TDisposed>(TDisposed disposed) => throw new ObjectDisposedException(disposed.GetType().FullName);
  }
}
