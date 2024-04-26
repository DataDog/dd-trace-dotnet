// <copyright file="AggregateExceptionRelatedFrames.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal class AggregateExceptionRelatedFrames : ExceptionRelatedFrames
    {
        public AggregateExceptionRelatedFrames(AggregateException ex, ParticipatingFrame[] frames, ExceptionRelatedFrames[] innerFrames)
            : base(ex, frames)
        {
            InnerFrames = innerFrames;
        }

        public ExceptionRelatedFrames[] InnerFrames { get; }

        public override IEnumerable<ParticipatingFrame> GetAllFlattenedFrames()
        {
            foreach (var frame in InnerFrames.SelectMany(innerFrame => innerFrame.GetAllFlattenedFrames()))
            {
                yield return frame;
            }

            foreach (var frame in Frames)
            {
                yield return frame;
            }
        }
    }
}
