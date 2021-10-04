// <copyright file="LoggingEventDuck.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.ComponentModel;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Log4Net.DirectSubmission
{
    /// <summary>
    /// Duck type for LoggingEvent
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class LoggingEventDuck
    {
        /// <summary>
        /// Gets the UTC time the event was logged
        /// </summary>
        public virtual DateTime TimeStampUtc { get; }

        /// <summary>
        /// Gets the name of the logger that logged the event.
        /// </summary>
        public virtual string LoggerName { get; }

        /// <summary>
        /// Gets the Level of the logging event
        /// </summary>
        public virtual LevelDuck Level { get; }

        /// <summary>
        /// Gets the application supplied message.
        /// </summary>
        public virtual string Message { get; }

        /// <summary>
        /// Gets the message, rendered through the RendererMap".
        /// </summary>
        public virtual string RenderedMessage { get; }

        /// <summary>
        /// Gets the exception object used to initialize this event
        /// </summary>
        public virtual Exception ExceptionObject { get; }

        /// <summary>
        /// Get all the composite properties in this event
        /// </summary>
        /// <returns>Dictionary containing all the properties</returns>
        public virtual IDictionary GetProperties()
        {
            return null;
        }
    }
}
