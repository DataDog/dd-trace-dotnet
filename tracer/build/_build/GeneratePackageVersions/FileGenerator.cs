// <copyright file="FileGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeneratePackageVersions
{
    public abstract class FileGenerator
    {
        public FileGenerator(string filename)
        {
            Filename = filename;
            FileStringBuilder = new StringBuilder();
        }

        protected abstract string Header { get; }

        protected abstract string Footer { get; }

        protected string Filename { get; private set; }

        protected bool Started { get; private set; }

        protected bool Finished { get; private set; }

        protected StringBuilder FileStringBuilder { get; private set; }

        public void Start()
        {
            Debug.Assert(!Started, "Cannot call Start() multiple times");

            FileStringBuilder.AppendLine(Header);
            Started = true;
        }

        public void Finish()
        {
            Debug.Assert(Started, "Cannot call Finish() before calling Start()");
            Debug.Assert(!Finished, "Cannot call Finish() multiple times");

            FileStringBuilder.AppendLine(Footer);
            File.WriteAllText(Filename, FileStringBuilder.ToString());
            Finished = true;
        }

        public abstract void Write(PackageVersionEntry packageVersionEntry, IEnumerable<(TargetFramework framework, IEnumerable<Version> versions)> versions, string requiresDockerDependency);
    }
}
