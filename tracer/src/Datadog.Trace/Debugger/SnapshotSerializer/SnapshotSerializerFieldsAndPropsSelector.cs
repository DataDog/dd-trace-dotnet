// <copyright file="SnapshotSerializerFieldsAndPropsSelector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Datadog.Trace.Debugger.SnapshotSerializer
{
    internal class SnapshotSerializerFieldsAndPropsSelector
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        private static readonly SnapshotSerializerFieldsAndPropsSelector Instance = new();
        private static readonly List<SnapshotSerializerFieldsAndPropsSelector> CustomSelectors =
            new()
            {
                new LazySnapshotSerializerFieldsAndPropsSelector(),
                new ExceptionSnapshotSerializerFieldsAndPropsSelector(),
                new TaskSnapshotSerializerFieldsAndPropsSelector(),
                new OldStyleTupleSnapshotSerializerFieldsAndPropsSelector()
            };

        protected SnapshotSerializerFieldsAndPropsSelector()
        {
        }

        internal static SnapshotSerializerFieldsAndPropsSelector CreateDeepClonerFieldsAndPropsSelector(Type type)
        {
            return CustomSelectors.FirstOrDefault(c => c.IsApplicable(type)) ?? Instance;
        }

        internal virtual bool IsApplicable(Type type) => true;

        /// <summary>
        /// Gets all fields and auto properties e.g. property with a backing field
        /// </summary>
        /// <param name="type">Object type</param>
        /// <param name="source">Object</param>
        /// <param name="maximumDepthOfHierarchyToCopy">Max depth of hierarchy</param>
        /// <param name="maximumNumberOfFieldsToCopy">Max fields</param>
        /// <param name="cts">Cancellation token source</param>
        /// <returns>Collection of fields and auto properties</returns>
        internal virtual IEnumerable<MemberInfo> GetFieldsAndProps(
            Type type,
            object source,
            int maximumDepthOfHierarchyToCopy,
            int maximumNumberOfFieldsToCopy,
            CancellationTokenSource cts)
        {
            return GetAllFields(type, maximumDepthOfHierarchyToCopy, cts).Take(maximumNumberOfFieldsToCopy).ToArray();
        }

        private static IEnumerable<FieldInfo> GetAllFields(Type type, int maximumDepthOfHierarchyToCopy, CancellationTokenSource cts)
        {
            int depth = 0;
            while (maximumDepthOfHierarchyToCopy == -1 || depth < maximumDepthOfHierarchyToCopy)
            {
                cts.Token.ThrowIfCancellationRequested();

                if (type == null)
                {
                    yield break;
                }

                foreach (var field in type.GetFields(Flags))
                {
                    yield return field;
                }

                depth++;
                type = type.BaseType;
            }
        }
    }
}
