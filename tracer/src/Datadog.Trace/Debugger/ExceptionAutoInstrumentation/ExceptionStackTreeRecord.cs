// <copyright file="ExceptionStackTreeRecord.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal class ExceptionStackTreeRecord
    {
        private readonly List<ExceptionStackNodeRecord> _methods;

        public ExceptionStackTreeRecord()
        {
            _methods = new List<ExceptionStackNodeRecord>();
        }

        public IList<ExceptionStackNodeRecord> Frames => _methods.AsReadOnly();

        public void Add(int level, TrackedStackFrameNode node)
        {
            _methods.Add(new ExceptionStackNodeRecord(level, node));
        }

        public void Add(ExceptionStackNodeRecord recordedMethodData)
        {
            _methods.Add(recordedMethodData);
        }
    }
}
