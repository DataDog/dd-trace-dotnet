using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.TestHelpers;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    /// <summary>
    /// Base class for all span expectations. Inherit from this class to extend per type or integration.
    /// </summary>
    public class SpanExpectation
    {
        public SpanExpectation(string serviceName, string operationName, string resourceName, string type)
        {
            ServiceName = serviceName;
            OperationName = operationName;
            ResourceName = resourceName;
            Type = type;

            // Expectations for all spans regardless of type should go here
            RegisterDelegateExpectation(ExpectBasicSpanDataExists);

            RegisterCustomExpectation(nameof(OperationName), actual: s => s.Name, expected: OperationName);
            RegisterCustomExpectation(nameof(ServiceName), actual: s => s.Service, expected: ServiceName);
            RegisterCustomExpectation(nameof(ResourceName), actual: s => s.Resource.ToLowerInvariant().TrimEnd(), expected: ResourceName.ToLowerInvariant());
            RegisterCustomExpectation(nameof(Type), actual: s => s.Type, expected: Type);

            RegisterTagExpectation(
                key: Tags.Language,
                expected: TracerConstants.Language,
                when: s => GetTag(s, Tags.SpanKind) != SpanKinds.Client);
        }

        public Func<MockTracerAgent.Span, bool> Always => s => true;

        public List<Func<MockTracerAgent.Span, string>> Assertions { get; } = new List<Func<MockTracerAgent.Span, string>>();

        public bool IsTopLevel { get; set; } = true;

        public string Type { get; set; }

        public string ResourceName { get; set; }

        public string OperationName { get; set; }

        public string ServiceName { get; set; }

        public static string GetTag(MockTracerAgent.Span span, string tag)
        {
            span.Tags.TryGetValue(tag, out var value);
            return value;
        }

        public virtual string Detail()
        {
            return $"service={ServiceName}, operation={OperationName}, type={Type}, resource={ResourceName}";
        }

        /// <summary>
        /// Override for custom filters.
        /// </summary>
        /// <param name="span">The span on which to filter.</param>
        /// <returns>Whether the span qualifies for this expectation.</returns>
        public virtual bool Matches(MockTracerAgent.Span span)
        {
            return span.Service == ServiceName
                && span.Name == OperationName
                && span.Type == Type;
        }

        /// <summary>
        /// The aggregate assertion which is run for a test.
        /// </summary>
        /// <param name="span">The span being asserted against.</param>
        /// <param name="message">The developer friendly message for the test failure.</param>
        /// <returns>Whether the span meets expectations.</returns>
        public bool MeetsExpectations(MockTracerAgent.Span span, out string message)
        {
            message = string.Empty;

            var messages = new List<string>();

            foreach (var assertion in Assertions)
            {
                var mismatchMessage = assertion(span);
                if (!string.IsNullOrWhiteSpace(mismatchMessage))
                {
                    messages.Add(mismatchMessage);
                }
            }

            if (messages.Any())
            {
                message = string.Join(",", messages);
                return false;
            }

            return true;
        }

        public void TagShouldExist(string tagKey, Func<MockTracerAgent.Span, bool> when)
        {
            Assertions.Add(span =>
            {
                if (when(span) && !span.Tags.ContainsKey(tagKey))
                {
                    return $"Tag {tagKey} is missing from span.";
                }

                return null;
            });
        }

        public void RegisterDelegateExpectation(Func<MockTracerAgent.Span, IEnumerable<string>> expectation)
        {
            if (expectation == null)
            {
                return;
            }

            Assertions.Add(span =>
            {
                var failures = expectation(span);

                if (failures != null && failures.Any())
                {
                    return string.Join(",", failures);
                }

                return null;
            });
        }

        public void RegisterCustomExpectation(
            string keyForMessage,
            Func<MockTracerAgent.Span, string> actual,
            string expected)
        {
            Assertions.Add(span =>
            {
                var actualValue = actual(span);

                if (expected != null && actualValue != expected)
                {
                    return FailureMessage(name: keyForMessage, actual: actualValue, expected: expected);
                }

                return null;
            });
        }

        public void RegisterTagExpectation(
            string key,
            string expected,
            Func<MockTracerAgent.Span, bool> when = null)
        {
            when = when ?? Always;

            Assertions.Add(span =>
            {
                if (!when(span))
                {
                    return null;
                }

                var actualValue = GetTag(span, key);

                if (expected != null && actualValue != expected)
                {
                    return FailureMessage(name: key, actual: actualValue, expected: expected);
                }

                return null;
            });
        }

        protected string FailureMessage(string name, string actual, string expected)
        {
            return $"({name} mismatch: actual: {actual ?? "NULL"}, expected: {expected ?? "NULL"})";
        }

        private IEnumerable<string> ExpectBasicSpanDataExists(MockTracerAgent.Span span)
        {
            if (string.IsNullOrWhiteSpace(span.Resource))
            {
                yield return "Resource must be set.";
            }

            if (string.IsNullOrWhiteSpace(span.Type))
            {
                yield return "Type must be set.";
            }

            if (string.IsNullOrWhiteSpace(span.Name))
            {
                yield return "Name must be set.";
            }

            if (string.IsNullOrWhiteSpace(span.Service))
            {
                yield return "Service must be set.";
            }

            if (span.TraceId == default)
            {
                yield return "TraceId must be set.";
            }

            if (span.SpanId == default)
            {
                yield return "SpanId must be set.";
            }
        }
    }
}
