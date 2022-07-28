// <copyright file="ExceptionSnapshotSerializerFieldsAndPropsSelector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Datadog.Trace.Debugger.Snapshots
{
    internal class ExceptionSnapshotSerializerFieldsAndPropsSelector : SnapshotSerializerFieldsAndPropsSelector
    {
        private readonly string[] _interestingProperties =
        {
            nameof(Exception.StackTrace),
            nameof(Exception.Message),
            nameof(Exception.InnerException),
            nameof(Exception.HResult),
            nameof(Exception.HelpLink),
            nameof(Exception.Source)
        };

        internal override bool IsApplicable(Type type)
        {
            return typeof(Exception).IsAssignableFrom(type);
        }

        internal override IEnumerable<MemberInfo> GetFieldsAndProps(
            Type type,
            object source,
            int maximumDepthOfHierarchyToCopy,
            int maximumNumberOfFieldsToCopy,
            CancellationTokenSource cts)
        {
            // Include the interesting (side-effect-free) properties from System.Exception
            var regularProps =
                typeof(Exception)
                   .GetProperties()
                   .Where(p => _interestingProperties.Contains(p.Name))
                   .Select(p => p);

            // Remove the fields declared on System.Exception - they are not interesting.
            var fields = base.GetFieldsAndProps(type, source, maximumDepthOfHierarchyToCopy, maximumNumberOfFieldsToCopy, cts)
                             .Where(b => b.DeclaringType != typeof(Exception));

            return regularProps.Concat(fields);
        }
    }
}
