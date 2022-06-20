// <copyright file="AttachProfilerCore.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.Principal;
using System.Text;
using Microsoft.Diagnostics.NETCore.Client;

namespace Datadog.Trace.Attach
{
    /// <summary>
    /// Allows the Datadog profiler to be loaded programmatically
    /// </summary>
    public static class AttachProfilerCore
    {
        private const ushort HeaderSizeInBytes = 20;
        private const string ProfilerPath = @"C:\code\dd-trace-dotnet\tracer\bin\dd-tracer-home\win-x64\Datadog.Trace.ClrProfiler.Native.dll";

        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);
        private static readonly Guid ProfilerGuid = Guid.Parse("{846F5F1C-F9AE-4B07-969E-05C26BC060D8}");

        /// <summary>
        /// Loads the profiler into the current process
        /// </summary>
        public static void LoadProfiler()
        {
            var proc = Process.GetCurrentProcess();
            var pid = proc.Id;

            LoadProfilerViaLib(pid);

            // LoadProfilerDirect(pid);
        }

        private static void LoadProfilerViaLib(int pid)
        {
            var client = new DiagnosticsClient(pid);
            client.AttachProfiler(Timeout, ProfilerGuid, ProfilerPath);
        }

        private static void LoadProfilerDirect(int pid)
        {
            var namedPipe = new NamedPipeClientStream(
                ".",
                "dotnet-diagnostic-" + pid,
                PipeDirection.InOut,
                PipeOptions.None,
                TokenImpersonationLevel.Impersonation);

            namedPipe.Connect((int)Timeout.TotalMilliseconds);

            WriteLoadProfilerMessage(namedPipe);
            ValidateResponse(namedPipe);
        }

        private static void ValidateResponse(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                var magic = reader.ReadBytes(14);
                Console.WriteLine(string.Join(", ", magic));
                var size = reader.ReadUInt16();
                Console.WriteLine(size);
                var commandSet = reader.ReadByte();
                Console.WriteLine(commandSet);
                var commandId = reader.ReadByte();
                Console.WriteLine(commandId);
                var reserved = reader.ReadUInt16();
                Console.WriteLine(reserved);
                var payload = reader.ReadBytes(size - HeaderSizeInBytes);
                Console.WriteLine(BitConverter.ToInt32(payload, 0).ToString("x"));

                if (commandId != 0)
                {
                    throw new Exception("Attach unsuccessful");
                }
            }
        }

        private static void WriteLoadProfilerMessage(Stream stream)
        {
            var dotnetIpcV1 = Encoding.ASCII.GetBytes("DOTNET_IPC_V1" + '\0');

            var headerSizeInBytesBytes = BitConverter.GetBytes(HeaderSizeInBytes);

            var profiler = (byte)0x03;

            var attachProfiler = (byte)0x01;
            var headerBytes = new[] { profiler, attachProfiler, (byte)0, (byte)0 };

            var attachProfilerBytes = BitConverter.GetBytes(attachProfiler);

            var additionData = (string)null;

            byte[] serializedConfiguration = SerializePayload(1000U, ProfilerGuid, ProfilerPath, additionData);

            stream.Write(dotnetIpcV1, 0, dotnetIpcV1.Length);
            stream.Write(headerSizeInBytesBytes, 0, headerSizeInBytesBytes.Length);
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(serializedConfiguration, 0, serializedConfiguration.Length);
        }

        private static byte[] SerializePayload<T1, T2, T3, T4>(T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                SerializePayloadArgument(arg1, writer);
                SerializePayloadArgument(arg2, writer);
                SerializePayloadArgument(arg3, writer);
                SerializePayloadArgument(arg4, writer);

                writer.Flush();
                return stream.ToArray();
            }
        }

        private static void SerializePayloadArgument<T>(T obj, BinaryWriter writer)
        {
            if (typeof(T) == typeof(string))
            {
                WriteString(writer, (string)((object)obj));
            }
            else if (typeof(T) == typeof(int))
            {
                writer.Write((int)((object)obj));
            }
            else if (typeof(T) == typeof(uint))
            {
                writer.Write((uint)((object)obj));
            }
            else if (typeof(T) == typeof(bool))
            {
                bool bValue = (bool)((object)obj);
                uint uiValue = bValue ? 1U : 0;
                writer.Write(uiValue);
            }
            else if (typeof(T) == typeof(Guid))
            {
                Guid guidVal = (Guid)((object)obj);
                writer.Write(guidVal.ToByteArray());
            }
            else if (typeof(T) == typeof(byte[]))
            {
                byte[] byteArray = (byte[])((object)obj);
                uint length = byteArray == null ? 0U : (uint)byteArray.Length;
                writer.Write(length);

                if (length > 0)
                {
                    writer.Write(byteArray);
                }
            }
            else
            {
                throw new ArgumentException($"Type {obj.GetType()} is not supported in SerializePayloadArgument, please add it.");
            }
        }

        private static void WriteString(BinaryWriter bw, string value)
        {
            if (bw == null)
            {
                throw new ArgumentNullException(nameof(bw));
            }

            bw.Write(value != null ? (value.Length + 1) : 0);
            if (value != null)
            {
                bw.Write(Encoding.Unicode.GetBytes(value + '\0'));
            }
        }
    }
}
#endif
