// <copyright file="ProcessEnvironmentLinux.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Datadog.Trace.Tools.Shared.Linux
{
    internal class ProcessEnvironmentLinux
    {
        public static IReadOnlyDictionary<string, string> ReadVariables(Process process)
        {
            /*
                   /proc/[pid]/environ
                          This file contains the environment for the process. The entries are separated by
                          null bytes ('\0'), and there may be a null byte at the end.
            */

            var path = $"/proc/{process.Id}/environ";

            var result = new Dictionary<string, string>();

            foreach (var line in File.ReadAllText(path).Split('\0', System.StringSplitOptions.RemoveEmptyEntries))
            {
                var values = line.Split('=', 2);
                result[values[0]] = values.Length > 1 ? values[1] : string.Empty;
            }

            return result;
        }

        public static string[] ReadModules(Process process) => ReadModulesFromFile($"/proc/{process.Id}/maps");

        internal static string[] ReadModulesFromFile(string path)
        {
            /*
                /proc/[pid]/maps
                A file containing the currently mapped memory regions and their access permissions.
                The format is:

                address           perms offset  dev   inode   pathname
                08048000-08056000 r-xp 00000000 03:0c 64593   /usr/sbin/gpm
                08056000-08058000 rw-p 0000d000 03:0c 64593   /usr/sbin/gpm
                08058000-0805b000 rwxp 00000000 00:00 0
                40000000-40013000 r-xp 00000000 03:0c 4165    /lib/ld-2.2.4.so
                40013000-40015000 rw-p 00012000 03:0c 4165    /lib/ld-2.2.4.so
                4001f000-40135000 r-xp 00000000 03:0c 45494   /lib/libc-2.2.4.so
                40135000-4013e000 rw-p 00115000 03:0c 45494   /lib/libc-2.2.4.so
                4013e000-40142000 rw-p 00000000 00:00 0
                bffff000-c0000000 rwxp 00000000 00:00 0
                where "address" is the address space in the process that it occupies, "perms" is a set of permissions:
                r = read
                w = write
                x = execute
                s = shared
                p = private (copy on write)
                "offset" is the offset into the file/whatever, "dev" is the device (major:minor), and "inode" is the inode on that device. 0 indicates that no inode is associated with the memory region, as the case would be with BSS (uninitialized data).
                Under Linux 2.0 there is no field giving pathname.
             */

            var modules = new HashSet<string>();

            foreach (var line in File.ReadAllLines(path))
            {
                var values = line.Split(' ', 6, System.StringSplitOptions.RemoveEmptyEntries);

                if (values.Length != 6)
                {
                    continue;
                }

                modules.Add(values[5]);
            }

            return modules.ToArray();
        }
    }
}
