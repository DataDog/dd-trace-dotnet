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
        // TODO/HACK copy/pasted with some replacements for SamplingPriority

        /// <summary>
        /// Sampling "priorities" indicate whether a trace should be kept (sampled) or dropped (not sampled).
        /// Trace statistics are computed based on all traces, even if they are dropped
        /// </summary>
        /// <remarks>
        /// <para>
        /// Currently, all traces are still sent to the Agent (for stats computation, etc),
        /// but this may change in future versions of the tracer.
        /// </para>
        /// <para>
        /// Despite the name, there is no relative priority between the different values.
        /// All the "keep" and "reject" values have the same weight, they only indicate where
        /// the decision originated from.
        /// </para>
        /// </remarks>
        public enum SamplingPriority
        {
            /// <summary>
            /// Trace should be dropped (not sampled).
            /// Sampling decision made explicitly by user through
            /// code or configuration (e.g. the rules sampler).
            /// </summary>
            UserReject = -1,

            /// <summary>
            /// Trace should be dropped (not sampled).
            /// Sampling decision made by the built-in sampler.
            /// </summary>
            AutoReject = 0,

            /// <summary>
            /// Trace should be kept (sampled).
            /// Sampling decision made by the built-in sampler.
            /// </summary>
            AutoKeep = 1,

            /// <summary>
            /// Trace should be kept (sampled).
            /// Sampling decision made explicitly by user through
            /// code or configuration (e.g. the rules sampler).
            /// </summary>
            UserKeep = 2,
        }

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

            // TODO hacky at first to get Tags
            // GetActivitySetTagAction needs to call the TraceContext.SetTag if it is set
            var setTagAction = GetActivitySetTagAction(activity, out var hasTraceContext);

            if (propagateId)
            {
                var base64UserId = Convert.ToBase64String(Encoding.UTF8.GetBytes(id));
                const string propagatedUserIdTag = "_dd.p." + "usr.id";
                setTagAction(propagatedUserIdTag, base64UserId);
            }
            else
            {
                setTagAction("usr.id", id);
            }

            if (email is not null)
            {
                setTagAction("usr.email", email);
            }

            if (name is not null)
            {
                setTagAction("usr.name", name);
            }

            if (sessionId is not null)
            {
                setTagAction("usr.session_id", sessionId);
            }

            if (role is not null)
            {
                setTagAction("usr.role", role);
            }

            if (scope is not null)
            {
                setTagAction("usr.scope", scope);
            }

            if (hasTraceContext)
            {
                RunBlockingCheck(activity, id);
            }
        }

        // TODO no clue what to do with the "out" here - changing it from signature of GetSpanSetter
        private static Action<string, object?> GetActivitySetTagAction(Activity activity, out bool hasTraceContext)
        {
            // TODO this should return the TraceContext.Tags.SetTag() function
            // TODO if there is no TraceContext it should return the activity?
            // TODO hack for now to just return the Activity
            hasTraceContext = false;
            // TODO if we have a TraceContext, hasTraceContext to true
            // then we need to use the action to set the TraceContext.Tags.SetTag() function
            // Hmm Tags.SetTag(string, string?) whereas Activity.SetTag(string, object?)
            Action<string, object?> setTag = (name, value) => activity.SetTag(name, value);
            return setTag;
        }

        private static void RunBlockingCheck(Activity span, string userId)
        {
            // TODO ex https://github.com/DataDog/dd-trace-dotnet/blob/master/tracer/src/Datadog.Trace/SpanExtensions.Framework.cs
            // TODO would need to rejit this to be able to access the ASM types
        }

        /// <summary>
        /// Sets the sampling priority for the trace that contains the specified <see cref="Activity"/>.
        /// </summary>
        /// <param name="activity">A span that belongs to the trace.</param>
        /// <param name="samplingPriority">The new sampling priority for the trace.</param>
        public static void SetTraceSamplingPriority(this Activity activity, SamplingPriority samplingPriority)
        {
            // TODO can/should stubs have code?
            if (activity == null) { throw new ArgumentNullException(nameof(activity)); }

            // TODO we need to get the TraceContext related to this Activity (if it exists)
            // then do a traceContext.SetSamplingPriority((int)samplingPriority, SamplingMechanism.Manual);
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

        /// <summary>
        /// Gets the currently active Span as an <see cref="Activity"/>.
        /// </summary>
        /// <returns>The currently active Span as an <see cref="Activity"/>; otherwise, <see langword="null"/>.</returns>
        /// <remarks>This isn't the same as the currently active <see cref="Activity"/> as this would allow exposure to
        /// the automatic instrumentation-generated Spans.</remarks>
        public static Activity? GetCurrentlyActiveActivity()
        {
            return null;
        }
    }
}
