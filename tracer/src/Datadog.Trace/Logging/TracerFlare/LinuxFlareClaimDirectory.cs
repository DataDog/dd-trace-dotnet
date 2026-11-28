// <copyright file="LinuxFlareClaimDirectory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Datadog.Trace.Logging.TracerFlare;

internal sealed class LinuxFlareClaimDirectory : IDisposable
{
    private const string ReceiptName = "flare-outcome";
    private const int WriteOnly = 0x1;
    private const int Create = 0x40;
    private const int Exclusive = 0x80;
    private const int NonBlocking = 0x800;
    private const int CloseOnExec = 0x80000;
    private const int PathOnly = 0x200000;
    private const int RemoveDirectory = 0x200;
    private const int AlreadyExists = 17;
    private const uint FileTypeMask = 0xf000;
    private const uint RegularFile = 0x8000;

    private static readonly bool IsArm64 = FrameworkDescription.Instance.ProcessArchitecture == ProcessArchitecture.Arm64;
    private static readonly int DirectoryOnly = IsArm64 ? 0x4000 : 0x10000;
    private static readonly int NoFollow = IsArm64 ? 0x8000 : 0x20000;
    private static readonly int DirectoryFlags = PathOnly | CloseOnExec | DirectoryOnly;
    private static readonly int Fstat = IsArm64 ? 80 : 5;

    private readonly SafeFileHandle _parent;
    private readonly SafeFileHandle _directory;
    private readonly string _claimName;

    private LinuxFlareClaimDirectory(SafeFileHandle parent, SafeFileHandle directory, string claimName)
    {
        _parent = parent;
        _directory = directory;
        _claimName = claimName;
    }

    public static bool IsSupported { get; } = FrameworkDescription.Instance.OSPlatform == OSPlatformName.Linux
                                          && FrameworkDescription.Instance.ProcessArchitecture is ProcessArchitecture.X64 or ProcessArchitecture.Arm64;

    public static LinuxFlareClaimDirectory? TryOpen(string claimPath, bool create = false)
    {
        if (!IsSupported)
        {
            return null;
        }

        var parent = new SafeFileHandle((IntPtr)Open(Path.GetDirectoryName(claimPath)!, DirectoryFlags), ownsHandle: true);
        SafeFileHandle? directory = null;
        var transferred = false;
        try
        {
            if (parent.IsInvalid)
            {
                return null;
            }

            var claimName = Path.GetFileName(claimPath);
            var parentFd = Descriptor(parent);
            if (create && MakeDirectoryAt(parentFd, claimName, 0b111_111_111) != 0 && Marshal.GetLastWin32Error() != AlreadyExists)
            {
                return null;
            }

            directory = new SafeFileHandle((IntPtr)OpenAt(parentFd, claimName, DirectoryFlags | NoFollow, 0), ownsHandle: true);
            if (directory.IsInvalid)
            {
                return null;
            }

            var claim = new LinuxFlareClaimDirectory(parent, directory, claimName);
            transferred = true;
            return claim;
        }
        finally
        {
            if (!transferred)
            {
                directory?.Dispose();
                parent.Dispose();
            }
        }
    }

    public FileStream? TryOpenReceipt(bool create = false)
    {
        var flags = NoFollow | CloseOnExec | (create ? WriteOnly | Create | Exclusive : NonBlocking);
        var handle = new SafeFileHandle((IntPtr)OpenAt(Descriptor(_directory), ReceiptName, flags, 0b110_110_110), ownsHandle: true);
        var transferred = false;
        try
        {
            if (handle.IsInvalid || (!create && !TryReadReceiptStatus(handle, out _)))
            {
                return null;
            }

            var stream = new FileStream(handle, create ? FileAccess.Write : FileAccess.Read, bufferSize: 1);
            transferred = true;
            return stream;
        }
        finally
        {
            if (!transferred)
            {
                handle.Dispose();
            }
        }
    }

    public void CleanIfExpired(long cutoffSeconds)
    {
        var directoryFd = Descriptor(_directory);
        if (ReadStatus(Fstat, directoryFd, out var directoryStatus) != 0 || directoryStatus.ModifiedSeconds >= cutoffSeconds)
        {
            return;
        }

        using var receipt = TryOpenReceipt();
        if (receipt is not null && (!TryReadReceiptStatus(receipt.SafeFileHandle, out var status) ||
                                    status.ModifiedSeconds >= cutoffSeconds ||
                                    UnlinkAt(directoryFd, ReceiptName, 0) != 0))
        {
            return;
        }

        // rmdir preserves directories that acquired a new receipt or contain other files.
        _ = UnlinkAt(Descriptor(_parent), _claimName, RemoveDirectory);
    }

    public void Dispose()
    {
        _directory.Dispose();
        _parent.Dispose();
    }

    private static int Descriptor(SafeFileHandle handle) => handle.DangerousGetHandle().ToInt32();

    private static bool TryReadReceiptStatus(SafeFileHandle handle, out KernelFileStatus status)
        => ReadStatus(Fstat, Descriptor(handle), out status) == 0 &&
           ((IsArm64 ? status.Arm64Mode : status.X64Mode) & FileTypeMask) == RegularFile;

    [DllImport("libc", EntryPoint = "open", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern int Open(string path, int flags);

    [DllImport("libc", EntryPoint = "openat", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern int OpenAt(int directory, string path, int flags, uint mode);

    [DllImport("libc", EntryPoint = "mkdirat", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern int MakeDirectoryAt(int directory, string path, uint mode);

    [DllImport("libc", EntryPoint = "unlinkat", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern int UnlinkAt(int directory, string path, int flags);

    [DllImport("libc", EntryPoint = "syscall", ExactSpelling = true, CallingConvention = CallingConvention.Cdecl)]
    private static extern nint ReadStatus(nint syscall, int descriptor, out KernelFileStatus status);

    // The Linux x64 kernel writes 144 bytes. ARM64 writes 128 bytes with the same mtime offset.
    [StructLayout(LayoutKind.Explicit, Size = 144)]
    private struct KernelFileStatus
    {
        [FieldOffset(16)]
        public uint Arm64Mode;

        [FieldOffset(24)]
        public uint X64Mode;

        [FieldOffset(88)]
        public long ModifiedSeconds;
    }
}
