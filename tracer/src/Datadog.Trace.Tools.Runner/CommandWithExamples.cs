// <copyright file="CommandWithExamples.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Help;

namespace Datadog.Trace.Tools.Runner
{
    internal class CommandWithExamples : Command
    {
        private readonly List<string> _examples = new();

        public CommandWithExamples(string name, string description = null)
            : base(name, description)
        {
        }

        public IReadOnlyList<string> Examples => _examples;

        public static HelpSectionDelegate ExamplesSection()
        {
            return GenerateExamplesSection;

            static void GenerateExamplesSection(HelpContext context)
            {
                if (context.Command is CommandWithExamples command && command.Examples.Count > 0)
                {
                    Console.WriteLine("Examples:");

                    foreach (var example in command.Examples)
                    {
                        Console.WriteLine($"  {example}");
                    }
                }
            }
        }

        public void AddExample(string example)
        {
            _examples.Add(example);
        }
    }
}
