// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Modified excerpt from dotnet/runtime. This version contains only
// the types, methods, and interfaces used by Vendored.System.Diagnostics.DiagnosticSource.Tests.csproj

using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Authentication;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Microsoft.Win32;

namespace Vendored.System.Diagnostics
{
    public static partial class PlatformDetection
    {
        //
        // Do not use the " { get; } = <expression> " pattern here. Having all the initialization happen in the type initializer
        // means that one exception anywhere means all tests using PlatformDetection fail. If you feel a value is worth latching,
        // do it in a way that failures don't cascade.
        //

        public static bool IsNetCore => Environment.Version.Major >= 5 || RuntimeInformation.FrameworkDescription.StartsWith(".NET Core", StringComparison.OrdinalIgnoreCase);

        public static bool IsThreadingSupported => true; // Not supported on Browser.
    }
}
