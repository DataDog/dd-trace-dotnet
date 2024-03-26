// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
#if (!NETFRAMEWORK)
using System.Text.Json;
#endif
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Datadog.Demos.Util;

#pragma warning disable SA1201 // Elements should appear in the correct order
#pragma warning disable SA1306 // Field names should begin with lower-case letter
#pragma warning disable SA1401 // Fields should be private

namespace Samples.FileAccess
{
    public enum Scenario
    {
        ReadWriteBinary = 1,
        ReadWriteText = 2,
        ReadWriteFileStream = 4,
        ReadWriteAsync = 8,
#if (!NETFRAMEWORK)
        ReadWriteLinesAsync = 16,
#endif
        ReadWriteXml = 32,
        ReadWriteXmlAsync = 64,
#if (!NETFRAMEWORK)
        ReadWriteJson = 128,
#endif
    }

    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine("######## Starting at " + DateTime.UtcNow);

            // supported scenarios:
            // --------------------
            //   1: synchronous write/read binary data through Binary(reader/writer)
            //   2: synchronous write/read textual data through Stream(reader/writer)
            //   4: synchronous write/read to FileStream
            //   8: asynchronous write/read
            //  16: asynchronous write/read lines
            //  32: synchronous write/read XML
            //  64: asynchronous write/read XML
            // 128: synchronous write/read JSON
            //
            Console.WriteLine($"{Environment.NewLine}Usage:{Environment.NewLine} > {Process.GetCurrentProcess().ProcessName} " +
            $"[--scenario <1=read/write binary 2=read/write text 4=read/write via FileStream 8=read/write async 16=read/write lines async 32=read/write XML 64=read/write async XML 128=read/write JSON] " +
            $"[--timeout <duration in seconds>]");
            Console.WriteLine();
            EnvironmentInfo.PrintDescriptionToConsole();

            ParseCommandLine(args, out TimeSpan timeout, out Scenario scenario);

            var cts = new CancellationTokenSource();

            var tasks = StartScenarios(scenario, cts.Token);

            if (timeout == TimeSpan.MinValue)
            {
                Console.WriteLine("Press ENTER to exit...");
                Console.ReadLine();
            }
            else
            {
                Thread.Sleep(timeout);
            }

            cts.Cancel();
            Task.WhenAll(tasks).Wait();

            Console.WriteLine($"{Environment.NewLine} ########### Finishing run at {DateTime.UtcNow}");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ReadWriteBinary(CancellationToken token)
        {
            var filename = Path.GetTempFileName();
            while (!token.IsCancellationRequested)
            {
                using (var stream = File.Open(filename, FileMode.OpenOrCreate))
                {
                    using (var writer = new BinaryWriter(stream, Encoding.UTF8, false))
                    {
                        writer.Write(DateTime.Now.ToShortTimeString());
                        for (Int32 i = 0; i < 10_000; i++)
                        {
                            writer.Write(i);
                        }

                        writer.Write("This is the end");
                    }
                }

                if (token.IsCancellationRequested)
                {
                    break;
                }

                using (var stream = File.Open(filename, FileMode.OpenOrCreate))
                {
                    using (var reader = new BinaryReader(stream, Encoding.UTF8, false))
                    {
                        reader.ReadString();
                        for (int i = 0; i < 10_000; i++)
                        {
                            reader.ReadInt32();
                        }

                        reader.ReadString();
                    }
                }
            }

            File.Delete(filename);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ReadWriteText(CancellationToken token)
        {
            var filename = Path.GetTempFileName();
            try
            {
                while (!token.IsCancellationRequested)
                {
                    using (var stream = File.Open(filename, FileMode.OpenOrCreate))
                    {
                        using (var writer = new StreamWriter(stream, Encoding.UTF8))
                        {
                            writer.WriteLine(DateTime.Now.ToShortTimeString());
                            for (Int32 i = 0; i < 10_000; i++)
                            {
                                writer.WriteLine(i);
                            }

                            writer.WriteLine("This is the end");
                        }
                    }

                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    using (var stream = File.Open(filename, FileMode.OpenOrCreate))
                    {
                        using (var reader = new StreamReader(stream, Encoding.UTF8))
                        {
                            reader.ReadLine();
                            for (int i = 0; i < 10_000; i++)
                            {
                                reader.ReadLine();
                            }

                            reader.ReadLine();
                        }
                    }
                }
            }
            catch (IOException x)
            {
                Console.WriteLine(x.ToString());
            }

            File.Delete(filename);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ReadWriteFileStream(CancellationToken token)
        {
            var filename = Path.GetTempFileName();
            try
            {
                while (!token.IsCancellationRequested)
                {
                    using (var stream = File.Open(filename, FileMode.OpenOrCreate))
                    {
                        byte[] buffer = new byte[10_000];
                        byte b = 0;
                        for (Int32 i = 0; i < 10_000; i++)
                        {
                            buffer[i] = b;

                            if (b == 255)
                            {
                                b = 0;
                            }
                            else
                            {
                                b++;
                            }
                        }

                        for (Int32 i = 0; i < 10_000; i++)
                        {
                            stream.Write(buffer, i, 1);
                        }
                    }

                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    using (var stream = File.Open(filename, FileMode.OpenOrCreate))
                    {
                        for (int i = 0; i < 10_000; i++)
                        {
                            stream.ReadByte();
                        }
                    }
                }
            }
            catch (IOException x)
            {
                Console.WriteLine(x.ToString());
            }

            File.Delete(filename);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task ReadWriteAsync(CancellationToken token)
        {
            var filename = Path.GetTempFileName();
            try
            {
                while (!token.IsCancellationRequested)
                {
                    using (var stream = new FileStream(
                                                filename,
                                                FileMode.OpenOrCreate,
                                                System.IO.FileAccess.ReadWrite,
                                                FileShare.ReadWrite,
                                                4096,
                                                true))
                    {
                        byte[] buffer = new byte[10_000];
                        byte b = 0;
                        for (Int32 i = 0; i < 10_000; i++)
                        {
                            buffer[i] = b;

                            if (b == 255)
                            {
                                b = 0;
                            }
                            else
                            {
                                b++;
                            }
                        }

                        for (Int32 i = 0; i < 10_000; i++)
                        {
                            await stream.WriteAsync(buffer, i, 1);
                        }
                    }

                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    using (var stream = new FileStream(
                                                filename,
                                                FileMode.OpenOrCreate,
                                                System.IO.FileAccess.ReadWrite,
                                                FileShare.ReadWrite,
                                                4096,
                                                true))
                    {
                        var buffer = new byte[1];
                        for (int i = 0; i < 10_000; i++)
                        {
                            await stream.ReadAsync(buffer, 0, 1);
                        }
                    }
                }
            }
            catch (IOException x)
            {
                Console.WriteLine(x.ToString());
            }

            File.Delete(filename);
        }

#if (!NETFRAMEWORK)
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task ReadWriteLineAsync(CancellationToken token)
        {
            var filename = Path.GetTempFileName();
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await File.WriteAllLinesAsync(filename, GetLines(), token);

                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    await File.ReadAllLinesAsync(filename);
                }
            }
            catch (IOException x)
            {
                Console.WriteLine(x.ToString());
            }

            File.Delete(filename);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IEnumerable<string> GetLines()
        {
            for (int i = 0; i < 10_000; i++)
            {
                yield return "data";
            }
        }
#endif

#if (NETFRAMEWORK)
        private static Task CompletedTask = Task.FromResult(false);
#endif
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task ReadWriteXml(CancellationToken token, bool asynchronous)
        {
            var filename = Path.GetTempFileName();
            try
            {
                var writerSettings = new XmlWriterSettings()
                {
                    Async = asynchronous,
                    OmitXmlDeclaration = true,
                    Indent = true,
                };

                var readerSettings = new XmlReaderSettings()
                {
                    Async = asynchronous,
                };

                while (!token.IsCancellationRequested)
                {
                    using (var writer = XmlWriter.Create(filename, writerSettings))
                    {
                        if (asynchronous)
                        {
                            await writer.WriteStartDocumentAsync();
                            await writer.WriteStartElementAsync(string.Empty, "data", string.Empty);
                        }
                        else
                        {
                            writer.WriteStartDocument();
                            writer.WriteStartElement("data");
                        }

                        for (int i = 0; i < 10_000; i++)
                        {
                            if (asynchronous)
                            {
                                await writer.WriteStartElementAsync(string.Empty, "person", string.Empty);
                                await writer.WriteElementStringAsync(string.Empty, "name", string.Empty, "john doe");
                                await writer.WriteEndElementAsync();
                            }
                            else
                            {
                                writer.WriteStartElement("person");
                                writer.WriteElementString("name", "john doe");
                                writer.WriteEndElement();
                            }
                        }

                        if (asynchronous)
                        {
                            await writer.WriteEndElementAsync();
                            await writer.WriteEndDocumentAsync();
                        }
                        else
                        {
                            writer.WriteEndElement();
                            writer.WriteEndDocument();
                        }
                    }

                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    using (var reader = XmlReader.Create(filename, readerSettings))
                    {
                        int elementCount = 0;
                        if (asynchronous)
                        {
                            while (await reader.ReadAsync())
                            {
                                switch (reader.NodeType)
                                {
                                    case XmlNodeType.Element:
                                        elementCount++;
                                        break;
                                }
                            }
                        }
                        else
                        {
                            while (reader.Read())
                            {
                                switch (reader.NodeType)
                                {
                                    case XmlNodeType.Element:
                                        elementCount++;
                                        break;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception x)
            {
                Console.WriteLine(x.ToString());
            }

            File.Delete(filename);

            if (!asynchronous)
            {
#if (NETFRAMEWORK)
                await CompletedTask;
#else
                await Task.CompletedTask;
#endif
            }
        }

#if (!NETFRAMEWORK)
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task ReadWriteJson(CancellationToken token, bool asynchronous)
        {
            var filename = Path.GetTempFileName();
            try
            {
                JsonSerializerOptions options = new JsonSerializerOptions()
                {
                    IncludeFields = true,
                    WriteIndented = true,
                };
                SerializableList list = new SerializableList();
                for (int i = 0; i < 10_000; i++)
                {
                    list.Add("name");
                }

                while (!token.IsCancellationRequested)
                {
                    using (var stream = File.Open(filename, FileMode.OpenOrCreate))
                    {
                        JsonSerializer.Serialize(stream, list, options);
                    }

                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    using (var stream = File.Open(filename, FileMode.OpenOrCreate))
                    {
                        var deserializedList = JsonSerializer.Deserialize(stream, typeof(SerializableList), options);
                    }
                }
            }
            catch (Exception x)
            {
                Console.WriteLine(x.ToString());
            }

            File.Delete(filename);

            if (!asynchronous)
            {
#if (NETFRAMEWORK)
                await CompletedTask;
#else
                await Task.CompletedTask;
#endif
            }
        }
#endif
        private static List<Task> StartScenarios(Scenario scenario, CancellationToken token)
        {
            List<Task> tasks = new List<Task>();
            if ((scenario & Scenario.ReadWriteBinary) == Scenario.ReadWriteBinary)
            {
                tasks.Add(
                    Task.Factory.StartNew(
                        () =>
                        {
                            ReadWriteBinary(token);
                        },
                        TaskCreationOptions.LongRunning));
            }

            if ((scenario & Scenario.ReadWriteText) == Scenario.ReadWriteText)
            {
                tasks.Add(
                    Task.Factory.StartNew(
                        () =>
                        {
                            ReadWriteText(token);
                        },
                        TaskCreationOptions.LongRunning));
            }

            if ((scenario & Scenario.ReadWriteFileStream) == Scenario.ReadWriteFileStream)
            {
                tasks.Add(
                    Task.Factory.StartNew(
                        () =>
                        {
                            ReadWriteFileStream(token);
                        },
                        TaskCreationOptions.LongRunning));
            }

            if ((scenario & Scenario.ReadWriteAsync) == Scenario.ReadWriteAsync)
            {
                tasks.Add(
                    Task.Factory.StartNew(
                        async () =>
                        {
                            await ReadWriteAsync(token);
                        },
                        TaskCreationOptions.LongRunning));
            }

#if (!NETFRAMEWORK)
            if ((scenario & Scenario.ReadWriteLinesAsync) == Scenario.ReadWriteLinesAsync)
            {
                tasks.Add(
                    Task.Factory.StartNew(
                        async () =>
                        {
                            await ReadWriteLineAsync(token);
                        },
                        TaskCreationOptions.LongRunning));
            }
#endif

            if ((scenario & Scenario.ReadWriteXml) == Scenario.ReadWriteXml)
            {
                tasks.Add(
                    Task.Factory.StartNew(
                        async () =>
                        {
                            await ReadWriteXml(token, asynchronous: false);
                        },
                        TaskCreationOptions.LongRunning));
            }

            if ((scenario & Scenario.ReadWriteXmlAsync) == Scenario.ReadWriteXmlAsync)
            {
                tasks.Add(
                    Task.Factory.StartNew(
                        async () =>
                        {
                            await ReadWriteXml(token, asynchronous: true);
                        },
                        TaskCreationOptions.LongRunning));
            }

#if (!NETFRAMEWORK)
            if ((scenario & Scenario.ReadWriteJson) == Scenario.ReadWriteJson)
            {
                tasks.Add(
                    Task.Factory.StartNew(
                        async () =>
                        {
                            await ReadWriteJson(token, asynchronous: true);
                        },
                        TaskCreationOptions.LongRunning));
            }
#endif
            return tasks;
        }

        private static void ParseCommandLine(string[] args, out TimeSpan timeout, out Scenario scenario)
        {
            timeout = TimeSpan.MinValue;
            scenario = Scenario.ReadWriteBinary;
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                if ("--timeout".Equals(arg, StringComparison.OrdinalIgnoreCase))
                {
                    int valueOffset = i + 1;
                    if (valueOffset < args.Length && int.TryParse(args[valueOffset], out var timeoutInSecond))
                    {
                        timeout = TimeSpan.FromSeconds(timeoutInSecond);
                    }
                }
                else
                if ("--scenario".Equals(arg, StringComparison.OrdinalIgnoreCase))
                {
                    int valueOffset = i + 1;
                    if (valueOffset < args.Length && int.TryParse(args[valueOffset], out var number))
                    {
                        scenario = (Scenario)number;
                    }
                }
            }
        }

        public class SerializableList
        {
            public List<string> Names = new List<string>();

            public void Add(string name)
            {
                Names.Add(name);
            }
        }
    }

#pragma warning restore SA1401 // Fields should be private
#pragma warning restore SA1306 // Field names should begin with lower-case letter
#pragma warning restore SA1201 // Elements should appear in the correct order
}
