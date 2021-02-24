using System;
using System.IO;

namespace Datadog.Trace.Ci
{
    /// <summary>
    /// Git information class
    /// </summary>
    internal class GitInfo
    {
        private GitInfo()
        {
        }

        /// <summary>
        /// Gets Source root
        /// </summary>
        public string SourceRoot { get; private set; }

        /// <summary>
        /// Gets Repository
        /// </summary>
        public string Repository { get; private set; }

        /// <summary>
        /// Gets Branch
        /// </summary>
        public string Branch { get; private set; }

        /// <summary>
        /// Gets Commit
        /// </summary>
        public string Commit { get; private set; }

        /// <summary>
        /// Gets a GitInfo from a folder
        /// </summary>
        /// <param name="folder">Target folder to retrieve the git info</param>
        /// <returns>Git info</returns>
        public static GitInfo GetFrom(string folder)
        {
            return GetFrom(new DirectoryInfo(folder));
        }

        /// <summary>
        /// Gets a GitInfo from the current folder or assembly attribute
        /// </summary>
        /// <returns>Git info</returns>
        public static GitInfo GetCurrent()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            DirectoryInfo gitDirectory = GetParentGitFolder(baseDirectory) ?? GetParentGitFolder(Environment.CurrentDirectory);
            return GetFrom(gitDirectory);
        }

        /// <summary>
        /// Gets the git info from a DirectoryInfo instance
        /// </summary>
        /// <param name="gitDirectory">DirectoryInfo instance</param>
        /// <returns>Git info</returns>
        private static GitInfo GetFrom(DirectoryInfo gitDirectory)
        {
            if (gitDirectory == null)
            {
                return new GitInfo();
            }

            GitInfo gitInfo = new GitInfo();

            try
            {
                gitInfo.SourceRoot = gitDirectory.Parent?.FullName;

                string refName = null;

                // Get Git commit
                string headPath = Path.Combine(gitDirectory.FullName, "HEAD");
                if (File.Exists(headPath))
                {
                    string head = File.ReadAllText(headPath).Trim();

                    // Symbolic Reference
                    if (head.StartsWith("ref:"))
                    {
                        refName = head.Substring(4).Trim();
                        string refPath = Path.Combine(gitDirectory.FullName, refName);
                        if (File.Exists(refPath))
                        {
                            gitInfo.Commit = File.ReadAllText(refPath).Trim();
                        }
                    }
                    else
                    {
                        // Hash reference
                        gitInfo.Commit = head;
                    }
                }

                // Process Git Config
                string configPath = Path.Combine(gitDirectory.FullName, "config");
                if (File.Exists(configPath))
                {
                    string[] configDataLines = File.ReadAllLines(configPath);
                    string remoteSearch = "origin";
                    string repository = string.Empty;
                    string branch = refName;
                    string tmpBranch = null;

                    // Extract Repository Url from that remote
                    bool intoRemote = false;
                    for (var i = 0; i < configDataLines.Length; i++)
                    {
                        string line = configDataLines[i];
                        if (line.StartsWith("[remote"))
                        {
                            intoRemote = line.Contains(remoteSearch);
                            continue;
                        }

                        if (branch == null && line.StartsWith("[branch"))
                        {
                            tmpBranch = line.Substring(9, line.Length - 11);
                            continue;
                        }

                        if (line.Contains("merge"))
                        {
                            int mergeIdx = line.IndexOf('=') + 1;
                            string mergeData = line.Substring(mergeIdx).Trim();
                            if (string.Equals(mergeData, refName, StringComparison.Ordinal))
                            {
                                branch = tmpBranch;
                                continue;
                            }
                        }

                        if (intoRemote && line.Contains("url ="))
                        {
                            string[] splitArray = line.Trim().Split(new[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                            if (splitArray.Length == 2)
                            {
                                repository = splitArray[1].Trim();
                            }
                        }
                    }

                    gitInfo.Repository = repository;
                    gitInfo.Branch = branch;
                }
            }
            catch
            {
            }

            return gitInfo;
        }

        /// <summary>
        /// Gets the Git Directory from an inner folder.
        /// </summary>
        /// <param name="innerFolder">Inner folder path</param>
        /// <returns>Directory info of the .git folder</returns>
        private static DirectoryInfo GetParentGitFolder(string innerFolder)
        {
            DirectoryInfo dirInfo = new DirectoryInfo(innerFolder);
            while (dirInfo != null)
            {
                DirectoryInfo[] gitDirectories = dirInfo.GetDirectories(".git");
                if (gitDirectories.Length > 0)
                {
                    foreach (var gitDir in gitDirectories)
                    {
                        if (gitDir.Name == ".git")
                        {
                            return gitDir;
                        }
                    }
                }

                dirInfo = dirInfo.Parent;
            }

            return null;
        }
    }
}
