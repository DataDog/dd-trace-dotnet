// <copyright file="ActivityExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Diagnostics;
using System.Text;

namespace Datadog.DiagnosticSource
{
    /*
     * public things not really covered by Activity:
     *  - SetException (kinda covered by Activity.SetStatus()), but not perfect match
     *  - SetUser - extension method for ASM
     *  - SetTraceSamplingPriority - needs to set the TraceContext sampling priority
     */

    /*
     * Things that require Datadog.Trace stuff
     * - SetTraceSamplingPriority(this Activity activity, SamplingPriority samplingPriority)
     *   - Needs to be able to map the Activity to the TraceContext
     * - SetUser:
     *   - GetActivitySetTagAction, needs to return the TraceContext.Tags.SetTag(string, string?) action when present
     *   - RunBlockingCheck(Activity span, string userId), needs to do more ASM stuff (Framework/Core differ slightly as well)
     * - Activity? GetCurrentlyActiveActivity():
     *   - To return (Span?)Tracer.Instance.ActiveScope?.Span but converted to an Activity
     *   - I _think_ we would also need a function to convert a Span into an Activity
     */

    /*
     * Uni-directional issue:
     *
     * Basically, we create Span for each Activity someone creates in manual instrumentation.
     * But we don't create/start a new Activity for each of our Spans.
     *
     * This allows us to "combine" our automatic instrumentation with their manual instrumentation.
     * But then users don't really have any way of getting to these automatic Spans without using Datadog.Trace
     *
     * Two ideas copy-pasted from Andrew:
     *   - Return an “Activity” that users interact with, and we implicitly delegate to our current active span
     *   - Use “simpler” APIs which don’t return anything. May be possible (and easier) in some respects,
     *     but also may result in a lot of “custom” APIs in use, when the whole point is to be removing
     *     public API that we’re maintaining.
     */

    /// <summary>
    ///     Extension-helper methods to provide Datadog-specific APIs to work with <see cref="Activity"/>.
    /// </summary>
    public static class ActivityExtensions
    {
        // TODO this needs to set tags on the TraceContext if possible

        /// <summary>
        /// Sets the details of the user on the local root span
        /// </summary>
        /// <param name="activity">The <see cref="Activity"/>.</param>
        /// <param name="id">The unique identifier associated with the users</param>
        /// <param name="propagateId">Gets or sets a value indicating whether the Id field should be propagated to other services called.</param>
        /// <param name="email">Gets or sets the user's email address</param>
        /// <param name="name">Gets or sets the user's name as displayed in the UI</param>
        /// <param name="sessionId">Gets or sets the user's session unique identifier</param>
        /// <param name="role">Gets or sets the role associated with the user</param>
        /// <param name="scope">Gets or sets the scopes or granted authorities the client currently possesses extracted from token or application security context</param>
        public static void SetUser(
                                   this Activity activity,
                                   string id,
                                   bool propagateId = false,
                                   string? email = null,
                                   string? name = null,
                                   string? sessionId = null,
                                   string? role = null,
                                   string? scope = null)
        {
            if (activity is null)
            {
                throw new ArgumentNullException(nameof(activity));
            }

            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException(nameof(id) + " must be set to a value other than null or the empty string", nameof(id));
            }
        }

        /// <summary>
        /// Sets the sampling priority for the trace that contains the specified <see cref="Activity"/>.
        /// </summary>
        /// <param name="activity">A span that belongs to the trace.</param>
        /// <param name="samplingPriority">The new sampling priority for the trace.</param>
        public static void SetTraceSamplingPriority(this Activity activity, SamplingPriority samplingPriority)
        {
            activity.AddTag("_sampling_priority_v1", samplingPriority);
        }

        /// <summary>
        ///     Mark <paramref name="activity"/> as <see cref="ActivityStatusCode.Error"/>
        ///     and append the <paramref name="exception"/> information as tags.
        /// </summary>
        /// <param name="activity">The <see cref="Activity"/></param>
        /// <param name="exception">The <see cref="Exception"/></param>
        public static void SetException(this Activity activity, Exception exception)
        {
            if (activity is null)
            {
                throw new ArgumentNullException(nameof(activity));
            }

            activity.SetStatus(ActivityStatusCode.Error);

            // TODO copy/pase from our Span
            if (exception is not null)
            {
                // for AggregateException, use the first inner exception until we can support multiple errors.
                // there will be only one error in most cases, and even if there are more and we lose
                // the other ones, it's still better than the generic "one or more errors occurred" message.
                if (exception is AggregateException aggregateException && aggregateException.InnerExceptions.Count > 0)
                {
                    exception = aggregateException.InnerExceptions[0];
                }

                activity.SetTag("error.msg", exception.Message);
                activity.SetTag("error.stack", exception.ToString());
                activity.SetTag("error.type", exception.GetType().ToString());
            }
        }
    }
}
