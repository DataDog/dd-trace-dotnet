using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Samples.AspNetCoreMvc.Controllers
{
    [Route("otel-baggage")]
    public class OtelBaggageController : ControllerBase
    {
        [HttpGet]
        [Route("clear-baggage")]
        public ActionResult ClearBaggage()
        {
            using var scope = SampleHelpers.CreateScope(nameof(ClearBaggage));
            var baggage = OpenTelemetry.Baggage.ClearBaggage().GetBaggage();
            var baggageString = string.Join(",", baggage.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            SampleHelpers.TrySetTag(scope, "otel-baggage", baggageString);

            return Ok(baggageString);
        }

        [HttpGet]
        [Route("get-baggage")]
        public ActionResult GetBaggage()
        {
            using var scope = SampleHelpers.CreateScope(nameof(GetBaggage));
            var baggage = OpenTelemetry.Baggage.GetBaggage();
            var baggageString = string.Join(",", baggage.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            SampleHelpers.TrySetTag(scope, "otel-baggage", baggageString);

            return Ok(baggageString);
        }

        [HttpGet]
        [Route("get-baggage-name/{key}")]
        public ActionResult GetBaggageName(string key)
        {
            using var scope = SampleHelpers.CreateScope(nameof(GetBaggageName));
            var foo_case_sensitive_key_value = OpenTelemetry.Baggage.GetBaggage("foo_case_sensitive_key");
            var baggageString = $"foo_case_sensitive_key={foo_case_sensitive_key_value ?? string.Empty}";
            SampleHelpers.TrySetTag(scope, "otel-baggage", baggageString);

            return Ok(baggageString);
        }

        [HttpGet]
        [Route("get-current")]
        public ActionResult GetCurrent()
        {
            using var scope = SampleHelpers.CreateScope(nameof(GetCurrent));
            var baggage = OpenTelemetry.Baggage.Current.GetBaggage();
            var baggageString = string.Join(",", baggage.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            SampleHelpers.TrySetTag(scope, "otel-baggage", baggageString);

            return Ok(baggageString);
        }

        [HttpGet]
        [Route("get-enumerator")]
        public ActionResult GetEnumerator()
        {
            using var scope = SampleHelpers.CreateScope(nameof(GetEnumerator));
            Dictionary<string, string> baggage = new();
            var enumerator = OpenTelemetry.Baggage.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var current = enumerator.Current;
                baggage.Add(current.Key, current.Value);
            }

            var baggageString = string.Join(",", baggage.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            SampleHelpers.TrySetTag(scope, "otel-baggage", baggageString);

            return Ok(baggageString);
        }


        [HttpGet]
        [Route("remove-baggage/{key}")]
        public ActionResult RemoveBaggage(string key)
        {
            using var scope = SampleHelpers.CreateScope(nameof(RemoveBaggage));
            var baggage = OpenTelemetry.Baggage.RemoveBaggage(key).GetBaggage();
            var baggageString = string.Join(",", baggage.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            SampleHelpers.TrySetTag(scope, "otel-baggage", baggageString);

            return Ok(baggageString);
        }

        [HttpGet]
        [Route("set-baggage/{key}/{value}")]
        public ActionResult SetBaggage(string key, string value)
        {
            using var scope = SampleHelpers.CreateScope(nameof(SetBaggage));
            var baggage = OpenTelemetry.Baggage.SetBaggage(key, value).GetBaggage();
            var baggageString = string.Join(",", baggage.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            SampleHelpers.TrySetTag(scope, "otel-baggage", baggageString);

            return Ok(baggageString);
        }

        [HttpGet]
        [Route("set-baggage-items/{key}/{value}")]
        public ActionResult SetBaggageItems(string key, string value)
        {
            using var scope = SampleHelpers.CreateScope(nameof(SetBaggageItems));
            var baggage = OpenTelemetry.Baggage.SetBaggage(new Dictionary<string, string>() { { key, value } }).GetBaggage();
            var baggageString = string.Join(",", baggage.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            SampleHelpers.TrySetTag(scope, "otel-baggage", baggageString);

            return Ok(baggageString);
        }

        [HttpGet]
        [Route("set-current/{key}/{value}")]
        public ActionResult SetCurrent(string key, string value)
        {
            using var scope = SampleHelpers.CreateScope(nameof(SetCurrent));
            var newBaggage = OpenTelemetry.Baggage.Create(new Dictionary<string, string>() { { key, value} });
            OpenTelemetry.Baggage.Current = newBaggage;

            var baggage = OpenTelemetry.Baggage.Current.GetBaggage();
            var baggageString = string.Join(",", baggage.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            SampleHelpers.TrySetTag(scope, "otel-baggage", baggageString);

            return Ok(baggageString);
        }
    }
}
