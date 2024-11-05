// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading;

namespace Microsoft.Configuration.ConfigurationBuilders
{
    /// <summary>
    /// Possible behaviors when dealing with config builder recursion.
    /// </summary>
    public enum RecursionGuardValues
    {
        /// <summary>
        /// [Default] Throw when reprocessing the same section.
        /// </summary>
        Throw,
        /// <summary>
        /// [!!May yield unexpected results in recursive conditions!!] Stop recursing, return without processing.
        /// </summary>
        Stop,
        /// <summary>
        /// [!!May result in deadlocks or stack overflow in recursive conditions!!] Do nothing. Continue recursing.
        /// </summary>
        Allow
    }

    /// <summary>
    /// Disposable object to help detect and handle problems with recursion.
    /// </summary>
    internal class RecursionGuard : IDisposable
    {
        private static readonly AsyncLocal<Stack<(string section, string builder)>> sectionsEntered = new AsyncLocal<Stack<(string, string)>>();

        internal bool ShouldStop = false;
        private readonly string sectionName;
        private readonly string builderName;

        internal RecursionGuard(ConfigurationBuilder configBuilder, string sectionName, RecursionGuardValues behavior)
        {
            // If behavior says "do nothing"... then let's do nothing.
            if (behavior == RecursionGuardValues.Allow)
                return;

            sectionsEntered.Value = sectionsEntered.Value ?? new Stack<(string, string)>();
            builderName = $"{configBuilder.Name}[{configBuilder.GetType()}]";

            // The idea here is that in this "thread" of execution, a config section is logged on this stack
            //   while being processed by a builder. Ie, in ProcessRawXml() or ProcessConfigurationSection().
            //   Once the builder is done in each phase, it pops the section off the stack. [And the next builder
            //   might pop it back on to do it's work.] The goal is to prevent an endless loop between sections
            //   in one thread, not to prevent concurrent processing by multiple threads.
            // If we ever re-enter the same config section recursively, it will already be on the stack.
            //   Since we push/pop for each builder, it does not cause problems with multiple builders in one chain.
            // Also, we can still enter a chain of different config sections so long as we don't re-enter any.
            // Unfortunately, we can't rely on much more than a simple section name since more information is
            //   not available to us, especially in ProcessRawXml(). Fortunately, config sections are generally
            //   top-level enough that there shouldn't really be any collisions here.
            var prev = FindSection(sectionsEntered.Value, sectionName);
            if (prev != null)
            {
                // Should we throw an exception?
                if (behavior == RecursionGuardValues.Throw)
                {
                    // Don't touch sectionsEntered. We did not add to it. We should not take from it or
                    // throw it away in case somebody wants to handle this exception.
                    //System.IO.File.WriteAllText(@"C:\ProgramData\Datadog .NET Tracer\logs\RecursionGuard.log", $"The ConfigurationBuilder '{prev.Value.builder}' has recursively re-entered processing of the '{sectionName}' section: " + new System.Diagnostics.StackFrame());
                    throw new InvalidOperationException($"The ConfigurationBuilder '{prev.Value.builder}' has recursively re-entered processing of the '{sectionName}' section.");
                }

                // If we don't throw, should we at least stop going down the rabbit hole?
                ShouldStop = (behavior == RecursionGuardValues.Stop);
            }

            // If we get here, then we will allow the section to be processed. Or at least we are returning
            // a guard instance and letting the caller decide what to do. We will be popping when we dispose,
            // so regardless of what our caller does, we should add the section name to the list of entered
            // sections, and also remember it so we know to remove it from the list when we are disposed.
            this.sectionName = sectionName;
            sectionsEntered.Value.Push((sectionName, builderName));
        }

        public void Dispose()
        {
            // If we tracked the entering of a section... stop tracking now.
            if (sectionName != null)
            {
                var (section, builder) = sectionsEntered.Value.Pop();

                // Sanity check to make sure we are un-tracking what we expect to un-track.
                if ((section != sectionName) || (builder != builderName)) {
                    throw new InvalidOperationException($"The ConfigurationBuilder {builderName} has detected a mix up while processing of the '{sectionName}' section. ({builder},{section})");
                }
            }
        }

        private static (string section, string builder)? FindSection(Stack<(string s, string b)> stack, string section)
        {
            foreach (var record in stack)
                if (record.s == section)
                    return record;

            return null;
        }
    }
}