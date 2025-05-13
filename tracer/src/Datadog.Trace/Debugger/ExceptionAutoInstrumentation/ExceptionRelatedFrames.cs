// <copyright file="ExceptionRelatedFrames.cs" company="Datadog">
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
    internal class ExceptionRelatedFrames
    {
        public ExceptionRelatedFrames(Exception ex, ParticipatingFrame[] frames)
        {
            Exception = ex;
            Frames = frames;
        }

        public ExceptionRelatedFrames(Exception ex, ParticipatingFrame[] frames, ExceptionRelatedFrames? innerFrame)
            : this(ex, frames)
        {
            InnerFrame = innerFrame;
        }

        public ParticipatingFrame[] Frames { get; }

        public Exception Exception { get; }

        public ExceptionRelatedFrames? InnerFrame { get; }

        public virtual IEnumerable<ParticipatingFrame> GetAllFlattenedFrames()
        {
            if (InnerFrame != null)
            {
                // Determine if we should skip the first method of the InnerException.
                // When an exception is placed as InnerException and rethrown, it leads to duplications of the rethrowing frame.
                // In other words, the last method of the outer exception is presented as the first method of the inner exception.
                // for example: outer trail: A -> B -> C, inner trail: C -> D -> E, to avoid being misled and think the full
                // exception trail is: A -> B -> C -> C -> D -> E (while we met C only once), we try to match the last method of the outer (this.Frames)
                // with the first method of the inner exception (this.InnerFrame). In case they match, we skip 1 frame so we'll have,
                // considering the previously mentioned example, the following trail: A -> B -> C -> D -> E, instead of A -> B -> C -> C -> D -> E.
                // For reference, see the following test: ExceptionCaughtAndRethrownAsInnerTest.

                var firstFrame = Frames?.FirstOrDefault();
                var lastFrameOfInner = InnerFrame.Frames?.Reverse().FirstOrDefault();

                var skipDuplicatedMethod = 0;
                if (lastFrameOfInner?.Method == firstFrame?.Method)
                {
                    skipDuplicatedMethod = 1;
                }

                foreach (var frame in InnerFrame.GetAllFlattenedFrames().Reverse().Skip(skipDuplicatedMethod).Reverse())
                {
                    yield return frame;
                }
            }

            if (Frames != null)
            {
                foreach (var frame in Frames)
                {
                    yield return frame;
                }
            }
        }
    }
}
