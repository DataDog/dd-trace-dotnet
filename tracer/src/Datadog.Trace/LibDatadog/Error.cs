// <copyright file="Error.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Runtime.InteropServices;
using System.Text;

namespace Datadog.Trace.LibDatadog;

[StructLayout(LayoutKind.Sequential)]
internal struct Error
{
    public VecU8 ErrorMessage;

    internal static string ReadAndDrop(ref Error resultErr)
    {
        var message = resultErr.ErrorMessage;
        if (message.Length == 0)
        {
            return string.Empty;
        }

        var buffer = new byte[(int)resultErr.ErrorMessage.Length];
        Marshal.Copy(message.Ptr, buffer, 0, (int)message.Length);

        var errorMessage = Encoding.UTF8.GetString(buffer);
        NativeInterop.Common.DropError(ref resultErr);
        return errorMessage;
    }
}
