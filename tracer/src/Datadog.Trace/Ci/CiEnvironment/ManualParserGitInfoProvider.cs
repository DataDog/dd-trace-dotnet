// <copyright file="ManualParserGitInfoProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Ci.CiEnvironment;

/// <summary>
/// Manual parser Git information class
/// </summary>
internal sealed class ManualParserGitInfoProvider : GitInfoProvider
{
    private ManualParserGitInfoProvider()
    {
    }

    public static IGitInfoProvider Instance { get; } = new ManualParserGitInfoProvider();

    protected override bool TryGetFrom(DirectoryInfo gitDirectory, [NotNullWhen(true)] out IGitInfo? gitInfo)
    {
        if (gitDirectory == null)
        {
            gitInfo = null;
            return false;
        }

        var tempGitInfo = new GitInfo();

        try
        {
            tempGitInfo.SourceRoot = gitDirectory.Parent?.FullName;

            // Get Git commit
            var headPath = Path.Combine(gitDirectory.FullName, "HEAD");
            if (File.Exists(headPath))
            {
                var head = File.ReadAllText(headPath).Trim();

                // Symbolic Reference
                if (head.StartsWith("ref:"))
                {
                    tempGitInfo.Branch = head.Substring(4).Trim();

                    var refPath = Path.Combine(gitDirectory.FullName, tempGitInfo.Branch);
                    var infoRefPath = Path.Combine(gitDirectory.FullName, "info", "refs");

                    if (File.Exists(refPath))
                    {
                        // Get the commit from the .git/{refPath} file.
                        tempGitInfo.Commit = File.ReadAllText(refPath).Trim();
                    }
                    else if (File.Exists(infoRefPath))
                    {
                        // Get the commit from the .git/info/refs file.
                        var lines = File.ReadAllLines(infoRefPath);
                        foreach (var line in lines)
                        {
                            var hashRef = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            if (hashRef[1] == tempGitInfo.Branch)
                            {
                                tempGitInfo.Commit = hashRef[0];
                            }
                        }
                    }
                }
                else
                {
                    // Hash reference
                    tempGitInfo.Commit = head;
                }
            }
            else
            {
                tempGitInfo.Errors.Add($"HEAD file not found in the git directory: {headPath}");
                gitInfo = null;
                return false;
            }

            // Process Git Config
            var configPath = Path.Combine(gitDirectory.FullName, "config");
            var lstConfigs = GetConfigItems(configPath);
            if (lstConfigs is { Count: > 0 })
            {
                var remote = "origin";

                var branchItem = lstConfigs.Find(i => i.Type == "branch" && i.Merge == tempGitInfo.Branch);
                if (branchItem != null)
                {
                    tempGitInfo.Branch = branchItem.Name;
                    remote = branchItem.Remote;
                }

                var remoteItem = lstConfigs.Find(i => i.Type == "remote" && i.Name == remote);
                if (remoteItem != null)
                {
                    tempGitInfo.Repository = remoteItem.Url;
                }
            }

            // Get author and committer data
            if (!string.IsNullOrEmpty(tempGitInfo.Commit))
            {
                var folder = tempGitInfo.Commit!.Substring(0, 2);
                var file = tempGitInfo.Commit!.Substring(2);
                var objectFilePath = Path.Combine(gitDirectory.FullName, "objects", folder, file);
                if (File.Exists(objectFilePath))
                {
                    // Load and parse object file
                    if (GitCommitObject.TryGetFromObjectFile(objectFilePath, tempGitInfo.Errors, out var commitObject))
                    {
                        tempGitInfo.AuthorDate = commitObject.AuthorDate;
                        tempGitInfo.AuthorEmail = commitObject.AuthorEmail;
                        tempGitInfo.AuthorName = commitObject.AuthorName;
                        tempGitInfo.CommitterDate = commitObject.CommitterDate;
                        tempGitInfo.CommitterEmail = commitObject.CommitterEmail;
                        tempGitInfo.CommitterName = commitObject.CommitterName;
                        tempGitInfo.Message = commitObject.Message;
                    }
                }
                else
                {
                    // Search git object file from the pack files
                    var packFolder = Path.Combine(gitDirectory.FullName, "objects", "pack");
                    var files = Directory.GetFiles(packFolder, "*.idx", SearchOption.TopDirectoryOnly);
                    foreach (var idxFile in files)
                    {
                        if (GitPackageOffset.TryGetPackageOffset(idxFile, tempGitInfo.Commit, tempGitInfo.Errors, out var packageOffset))
                        {
                            if (GitCommitObject.TryGetFromPackageOffset(packageOffset, tempGitInfo.Errors, out var commitObject))
                            {
                                tempGitInfo.AuthorDate = commitObject.AuthorDate;
                                tempGitInfo.AuthorEmail = commitObject.AuthorEmail;
                                tempGitInfo.AuthorName = commitObject.AuthorName;
                                tempGitInfo.CommitterDate = commitObject.CommitterDate;
                                tempGitInfo.CommitterEmail = commitObject.CommitterEmail;
                                tempGitInfo.CommitterName = commitObject.CommitterName;
                                tempGitInfo.Message = commitObject.Message;
                                break;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            tempGitInfo.Errors.Add($"GitInfo: Error loading git information from directory: {ex}");
            gitInfo = null;
            return false;
        }

        tempGitInfo.Branch = tempGitInfo.Branch?.Replace("refs/heads/", string.Empty);
        gitInfo = tempGitInfo;
        return true;
    }

    private static List<ConfigItem>? GetConfigItems(string configFile)
    {
        if (!File.Exists(configFile))
        {
            return null;
        }

        var lstConfig = new List<ConfigItem>();
        ConfigItem? currentItem = null;

        var regex = new Regex("^\\[(.*) \\\"(.*)\\\"\\]");
        var lines = File.ReadAllLines(configFile);
        foreach (var line in lines)
        {
            if (line[0] == '\t')
            {
                if (currentItem != null)
                {
                    var keyValue = line.Substring(1).Split([" = "], StringSplitOptions.RemoveEmptyEntries);
                    switch (keyValue[0])
                    {
                        case "url":
                            currentItem.Url = keyValue[1];
                            break;
                        case "remote":
                            currentItem.Remote = keyValue[1];
                            break;
                        case "merge":
                            currentItem.Merge = keyValue[1];
                            break;
                    }
                }

                continue;
            }

            var match = regex.Match(line);
            if (match.Success)
            {
                if (currentItem != null)
                {
                    lstConfig.Add(currentItem);
                }

                currentItem = new ConfigItem
                {
                    Type = match.Groups[1].Value,
                    Name = match.Groups[2].Value
                };
            }
        }

        return lstConfig;
    }

    internal readonly struct GitCommitObject
    {
        public readonly string? Tree;
        public readonly string? Parent;
        public readonly string? AuthorName;
        public readonly string? AuthorEmail;
        public readonly DateTimeOffset? AuthorDate;
        public readonly string? CommitterName;
        public readonly string? CommitterEmail;
        public readonly DateTimeOffset? CommitterDate;
        public readonly string? PgpSignature;
        public readonly string? Message;

        private const string TreePrefix = "tree ";
        private const string ParentPrefix = "parent ";
        private const string AuthorPrefix = "author ";
        private const string CommitterPrefix = "committer ";
        private const string GpgSigPrefix = "gpgsig ";
        private const long UnixEpochTicks = TimeSpan.TicksPerDay * 719162; // 621,355,968,000,000,000

        private static readonly byte[] CommitByteArray = Encoding.UTF8.GetBytes("commit");

        private GitCommitObject(string content)
        {
            Tree = null;
            Parent = null;
            AuthorName = null;
            AuthorEmail = null;
            AuthorDate = null;
            CommitterName = null;
            CommitterEmail = null;
            CommitterDate = null;
            PgpSignature = null;
            Message = null;

            var lines = content.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var msgLines = new List<string>();
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                if (line.StartsWith(TreePrefix))
                {
                    Tree = line.Substring(TreePrefix.Length);
                    continue;
                }

                if (line.StartsWith(ParentPrefix))
                {
                    Parent = line.Substring(ParentPrefix.Length);
                    continue;
                }

                if (line.StartsWith(AuthorPrefix))
                {
                    var authorContent = line.Substring(AuthorPrefix.Length);
                    var authorArray = authorContent.Split('<', '>');
                    AuthorName = authorArray[0].Trim();
                    AuthorEmail = authorArray[1].Trim();
                    var authorDate = authorArray[2].Trim();
                    var authorDateArray = authorDate.Split(' ');
                    if (long.TryParse(authorDateArray[0], out var unixSeconds))
                    {
                        AuthorDate = new DateTimeOffset((unixSeconds * TimeSpan.TicksPerSecond) + UnixEpochTicks, TimeSpan.Zero);
                    }

                    continue;
                }

                if (line.StartsWith(CommitterPrefix))
                {
                    var committerContent = line.Substring(CommitterPrefix.Length);
                    var committerArray = committerContent.Split('<', '>');
                    CommitterName = committerArray[0].Trim();
                    CommitterEmail = committerArray[1].Trim();
                    var committerDate = committerArray[2].Trim();
                    var committerDateArray = committerDate.Split(' ');
                    if (long.TryParse(committerDateArray[0], out var unixSeconds))
                    {
                        CommitterDate = new DateTimeOffset((unixSeconds * TimeSpan.TicksPerSecond) + UnixEpochTicks, TimeSpan.Zero);
                    }

                    continue;
                }

                if (line.StartsWith(GpgSigPrefix))
                {
                    var pgpLine = line.Substring(GpgSigPrefix.Length) + Environment.NewLine;
                    PgpSignature = pgpLine;
                    while (!pgpLine.Contains("END PGP SIGNATURE") && !pgpLine.Contains("END SSH SIGNATURE") && i + 1 < lines.Length)
                    {
                        i++;
                        pgpLine = lines[i];
                        PgpSignature += pgpLine + Environment.NewLine;
                    }

                    continue;
                }

                msgLines.Add(line.Trim());
            }

            Message = string.Join(Environment.NewLine, msgLines);
        }

        public static bool TryGetFromObjectFile(string filePath, List<string> errors, out GitCommitObject commitObject)
        {
            commitObject = default;
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

                // We skip the 2 bytes zlib header magic number.
                fs.Seek(2, SeekOrigin.Begin);
                using var defStream = new DeflateStream(fs, CompressionMode.Decompress);
                var buffer = new byte[8192];
                var readBytes = defStream.Read(buffer, 0, buffer.Length);
                defStream.Close();

                if (CommitByteArray.SequenceEqual(buffer.Take(CommitByteArray.Length)))
                {
                    var strContent = Encoding.UTF8.GetString(buffer, 0, readBytes);
                    var dataContent = strContent.Substring(strContent.IndexOf('\0') + 1);
                    commitObject = new GitCommitObject(dataContent);
                    return true;
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Error getting commit object from object file: {ex}");
            }

            return false;
        }

        public static bool TryGetFromPackageOffset(GitPackageOffset packageOffset, List<string> errors, out GitCommitObject commitObject)
        {
            commitObject = default;
            try
            {
                var packFile = Path.ChangeExtension(packageOffset.FilePath, ".pack");
                if (File.Exists(packFile))
                {
                    // packfile format explanation:
                    // https://codewords.recurse.com/issues/three/unpacking-git-packfiles#:~:text=idx%20file%20contains%20the%20index,pack%20file.&text=Objects%20in%20a%20packfile%20can,of%20storing%20the%20whole%20object.

                    using var fs = new FileStream(packFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var br = new BigEndianBinaryReader(fs);

                    // Move to the offset of the object
                    fs.Seek(packageOffset.Offset, SeekOrigin.Begin);
                    var packData = br.ReadBytes(2);

                    // Extract the object size (https://codewords.recurse.com/images/three/varint.svg)
                    var objectSize = (int)(packData[0] & 0x0F);
                    if (packData[0] >= 128)
                    {
                        int shift = 4;
                        objectSize += (packData[1] & 0x7F) << shift;
                        if (packData[1] >= 128)
                        {
                            byte pData;
                            do
                            {
                                shift += 7;
                                pData = br.ReadByte();
                                objectSize += (pData & 0x7F) << shift;
                            }
                            while (pData >= 128);
                        }
                    }

                    // Check if the object size is in the aceptable range
                    if (objectSize is > 0 and < ushort.MaxValue)
                    {
                        // Advance 2 bytes to skip the zlib magic number
                        uint zlibMagicNumber = br.ReadUInt16();
                        if ((byte)zlibMagicNumber == 0x78)
                        {
                            // Read the git commit object
                            using var defStream = new DeflateStream(br.BaseStream, CompressionMode.Decompress);
                            var buffer = new byte[objectSize];
                            var readBytes = defStream.Read(buffer, 0, buffer.Length);
                            defStream.Close();
                            var strContent = Encoding.UTF8.GetString(buffer, 0, readBytes);
                            commitObject = new GitCommitObject(strContent);
                            return true;
                        }
                        else
                        {
                            errors.Add($"The commit data doesn't have a valid zlib header magic number. [Received: 0x{zlibMagicNumber:X2}, Expected: 0x78]");
                        }
                    }
                    else
                    {
                        errors.Add($"The object size is outside of an acceptable range: {objectSize}");
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Error getting commit object from package offset: {ex}");
            }

            return false;
        }
    }

    internal readonly struct GitPackageOffset
    {
        public readonly string FilePath;
        public readonly long Offset;

        internal GitPackageOffset(string filePath, long offset)
        {
            FilePath = filePath;
            Offset = offset;
        }

        public static bool TryGetPackageOffset(string idxFilePath, string commitSha, List<string> errors, out GitPackageOffset packageOffset)
        {
            packageOffset = default;

            // packfile format explanation:
            // https://codewords.recurse.com/issues/three/unpacking-git-packfiles#:~:text=idx%20file%20contains%20the%20index,pack%20file.&text=Objects%20in%20a%20packfile%20can,of%20storing%20the%20whole%20object.

            var index = commitSha.Substring(0, 2);
            var folderIndex = int.Parse(index, System.Globalization.NumberStyles.HexNumber);
            var previousIndex = folderIndex > 0 ? folderIndex - 1 : folderIndex;

            try
            {
                using var fs = new FileStream(idxFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var br = new BigEndianBinaryReader(fs);

                // Skip header and version
                fs.Seek(8, SeekOrigin.Begin);

                // First layer: 256 4-byte elements, with number of elements per folder
                uint numberOfObjectsInPreviousIndex = 0;
                if (previousIndex > -1)
                {
                    // Seek to previous index position and read the number of objects
                    fs.Seek(previousIndex * 4, SeekOrigin.Current);
                    numberOfObjectsInPreviousIndex = br.ReadUInt32();
                }

                // In the fanout table, every index has its objects + the previous ones.
                // We need to subtract the previous index objects to know the correct
                // actual number of objects for this specific index.
                var numberOfObjectsInIndex = br.ReadUInt32() - numberOfObjectsInPreviousIndex;

                // Seek to last position. The last position contains the number of all objects.
                fs.Seek((255 - (folderIndex + 1)) * 4, SeekOrigin.Current);
                var totalNumberOfObjects = br.ReadUInt32();

                // Second layer: 20-byte elements with the names in order
                // Search the sha index in the second layer: the SHA listing.
                uint? indexOfCommit = null;
                fs.Seek(20 * (int)numberOfObjectsInPreviousIndex, SeekOrigin.Current);
                for (uint i = 0; i < numberOfObjectsInIndex; i++)
                {
                    var str = BitConverter.ToString(br.ReadBytes(20)).Replace("-", string.Empty);
                    if (str.Equals(commitSha, StringComparison.OrdinalIgnoreCase))
                    {
                        indexOfCommit = numberOfObjectsInPreviousIndex + i;

                        // If we find the SHA, we skip all SHA listing table.
                        fs.Seek(20 * (totalNumberOfObjects - (indexOfCommit.Value + 1)), SeekOrigin.Current);
                        break;
                    }
                }

                if (indexOfCommit.HasValue)
                {
                    // Third layer: 4 byte CRC for each object. We skip it
                    fs.Seek(4 * totalNumberOfObjects, SeekOrigin.Current);

                    var indexOfCommitValue = indexOfCommit.Value;

                    // Fourth layer: 4 byte per object of offset in pack file
                    fs.Seek(4 * indexOfCommitValue, SeekOrigin.Current);
                    var offset = br.ReadUInt32();

                    ulong packOffset;
                    if (((offset >> 31) & 1) == 0)
                    {
                        // offset is in the layer
                        packOffset = (ulong)offset;
                    }
                    else
                    {
                        // offset is not in this layer, clear first bit and look at it at the 5th layer
                        offset &= 0x7FFFFFFF;
                        // Skip complete fourth layer.
                        fs.Seek(4 * (totalNumberOfObjects - (indexOfCommitValue + 1)), SeekOrigin.Current);
                        // Use the offset from fourth layer, to find the actual pack file offset in the fifth layer.
                        // In this case, the offset is 8 bytes long.
                        fs.Seek(8 * offset, SeekOrigin.Current);
                        packOffset = br.ReadUInt64();
                    }

                    packageOffset = new GitPackageOffset(idxFilePath, (long)packOffset);
                    return true;
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Error getting package offset: {ex}");
            }

            return false;
        }
    }

    internal sealed class ConfigItem
    {
        public string? Type { get; set; }

        public string? Name { get; set; }

        public string? Url { get; set; }

        public string? Remote { get; set; }

        public string? Merge { get; set; }
    }

    internal sealed class BigEndianBinaryReader(Stream stream) : BinaryReader(stream)
    {
        public override int ReadInt32()
        {
            var data = ReadBytes(4);
            Array.Reverse(data);
            return BitConverter.ToInt32(data, 0);
        }

        public override short ReadInt16()
        {
            var data = ReadBytes(2);
            Array.Reverse(data);
            return BitConverter.ToInt16(data, 0);
        }

        public override long ReadInt64()
        {
            var data = ReadBytes(8);
            Array.Reverse(data);
            return BitConverter.ToInt64(data, 0);
        }

        public override uint ReadUInt32()
        {
            var data = ReadBytes(4);
            Array.Reverse(data);
            return BitConverter.ToUInt32(data, 0);
        }
    }
}
