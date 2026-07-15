// <copyright file="AttributeRoutesController.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace;
using Microsoft.AspNetCore.Mvc;

namespace Samples.AspNetCoreNetFramework
{
    [Route("attribute")]
    public class AttributeRoutesController : ControllerBase
    {
        private const string CorrelationIdentifierHeader = "x-legacy-correlation-id";
        private const string ResponseHeader = "x-legacy-response-header";

        [HttpGet("items/{id}")]
        public IActionResult Item(string id) => Ok(id);

        [HttpGet("response-header")]
        public IActionResult AddResponseHeader()
        {
            Response.Headers[ResponseHeader] = "response-value";
            return Ok();
        }

        [HttpGet("status/{statusCode:int}")]
        public IActionResult Status(int statusCode) => StatusCode(statusCode, $"Status code has been set to {statusCode}");

        [HttpGet("error")]
        public IActionResult Error() => throw new InvalidOperationException("Unhandled MVC request failure");

        [HttpGet("delay/{milliseconds:int}")]
        public async Task<IActionResult> Delay(int milliseconds)
        {
            if (milliseconds < 0 || milliseconds > 5_000)
            {
                return BadRequest("Delay must be between 0 and 5000 milliseconds.");
            }

            await Task.Delay(milliseconds);

            if (Request.Headers.TryGetValue(CorrelationIdentifierHeader, out var correlationIdentifier))
            {
                Response.Headers[CorrelationIdentifierHeader] = correlationIdentifier;
            }

            return Ok(milliseconds);
        }

        [HttpGet("baggage/{key}")]
        public IActionResult GetBaggage(string key)
        {
            Baggage.Current.TryGetValue(key, out var value);
            return Content(value ?? string.Empty);
        }
    }
}
