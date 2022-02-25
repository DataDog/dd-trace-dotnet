// <copyright file="Link.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Text;
using System.Threading;

namespace BuggyBits.Models
{
    public class Link
    {
        public Link(string title, string url)
        {
            this.Title = title;
            this.URL.Append(url);
        }

        ~Link()
        {
            // Some long running operation when cleaning up the data
            Thread.Sleep(5000);
        }

        public StringBuilder URL { get; set; } = new StringBuilder(10000);
        public string Title { get; set; }
    }
}
