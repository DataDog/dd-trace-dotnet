// <copyright file="RaspController.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Samples.Security.AspNetCore5.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class RaspController : ControllerBase
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        [HttpGet("DownstreamRequest")]
        public async Task<IActionResult> DownstreamRequest(string url, string body = null)
        {
            try
            {
                HttpResponseMessage response;

                if (!string.IsNullOrEmpty(body))
                {
                    var content = new StringContent(body, Encoding.UTF8, "application/json");
                    response = await _httpClient.PostAsync(url, content);
                }
                else
                {
                    response = await _httpClient.GetAsync(url);
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                return Ok(new
                {
                    StatusCode = (int)response.StatusCode,
                    Body = responseBody
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    Error = ex.Message
                });
            }
        }

        [HttpGet("DownstreamResponse")]
        public async Task<IActionResult> DownstreamResponse(string url)
        {
            try
            {
                var response = await _httpClient.GetAsync(url);
                var responseBody = await response.Content.ReadAsStringAsync();

                return Ok(new
                {
                    StatusCode = (int)response.StatusCode,
                    Body = responseBody
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    Error = ex.Message
                });
            }
        }

        [HttpGet("DownstreamMultiple")]
        public async Task<IActionResult> DownstreamMultiple(int count, string url)
        {
            try
            {
                int successCount = 0;
                int failureCount = 0;

                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        var response = await _httpClient.GetAsync(url);
                        if (response.IsSuccessStatusCode)
                        {
                            successCount++;
                        }
                        else
                        {
                            failureCount++;
                        }
                    }
                    catch
                    {
                        failureCount++;
                    }
                }

                return Ok(new
                {
                    TotalRequests = count,
                    Successful = successCount,
                    Failed = failureCount
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    Error = ex.Message
                });
            }
        }

        [HttpGet("DownstreamLargeBody")]
        public async Task<IActionResult> DownstreamLargeBody(string url, int sizeBytes)
        {
            try
            {
                // Generate a large JSON body
                var sb = new StringBuilder();
                sb.Append("{\"data\":\"");
                sb.Append(new string('a', sizeBytes - 12)); // Account for JSON overhead
                sb.Append("\"}");

                var content = new StringContent(sb.ToString(), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                return Ok(new
                {
                    StatusCode = (int)response.StatusCode,
                    BodySize = sizeBytes,
                    Response = responseBody.Length > 100 ? responseBody.Substring(0, 100) + "..." : responseBody
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    Error = ex.Message
                });
            }
        }

#if NETCOREAPP3_0_OR_GREATER
        // Returns a simple JSON response for use as a local downstream target.
        // GET: echoes the query string body param back with added status and length fields.
        // POST: echoes the request body back with added status and length fields.
        [HttpGet("Echo")]
        public IActionResult EchoGet([FromQuery] string body = null)
        {
            if (string.IsNullOrEmpty(body))
            {
                return Ok(new { status = "ok", length = 0, body = (object)null });
            }

            var parsedBody = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(body);
            return Ok(new { status = "ok", length = body.Length, body = parsedBody });
        }

        [HttpPost("Echo")]
        public IActionResult EchoPost([FromBody] System.Text.Json.JsonElement body)
        {
            return Ok(new { status = "ok", length = body.GetRawText().Length, body });
        }

        // API10: reads the downstream URL from the request body (user-controlled) and makes the call.
        // This is SSRF-vulnerable by design; the WAF fires because server.request.body taints server.io.net.url.
        [HttpPost("DownstreamFromBody")]
        public async Task<IActionResult> DownstreamFromBody([FromBody] System.Text.Json.JsonElement body)
        {
            try
            {
                var url = body.GetProperty("url").GetString();
                var response = await _httpClient.GetAsync(url);
                var responseBody = await response.Content.ReadAsStringAsync();
                return Ok(new { StatusCode = (int)response.StatusCode, Body = responseBody });
            }
            catch (Exception ex)
            {
                return Ok(new { Error = ex.Message });
            }
        }
#endif

        // Makes a downstream GET (or POST when body query param is set) to this app's own Echo endpoint.
        // The URL is built server-side so it does not flow from user input, avoiding SSRF detection.
        [HttpGet("DownstreamToSelf")]
        public async Task<IActionResult> DownstreamToSelf([FromQuery] string body = null)
        {
            var echoUrl = $"{Request.Scheme}://{Request.Host}/Rasp/Echo";
            try
            {
                HttpResponseMessage response;
                if (!string.IsNullOrEmpty(body))
                {
                    var content = new StringContent(body, Encoding.UTF8, "application/json");
                    response = await _httpClient.PostAsync(echoUrl, content);
                }
                else
                {
                    body = "{ \"content\": \"defaultBody\", \"aws\": \"credentials\" }";
                    response = await _httpClient.GetAsync(echoUrl + $"?body={body}");
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                return Ok(new { StatusCode = (int)response.StatusCode, Body = responseBody });
            }
            catch (Exception ex)
            {
                return Ok(new { Error = ex.Message });
            }
        }

        // Makes a downstream POST to Echo using the incoming request body
        [HttpPost("DownstreamToSelf")]
        public async Task<IActionResult> DownstreamToSelfPost()
        {
            var echoUrl = $"{Request.Scheme}://{Request.Host}/Rasp/Echo";
            try
            {
                using var reader = new StreamReader(Request.Body);
                var body = await reader.ReadToEndAsync();
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(echoUrl, content);
                var responseBody = await response.Content.ReadAsStringAsync();
                return Ok(new { StatusCode = (int)response.StatusCode, Body = responseBody });
            }
            catch (Exception ex)
            {
                return Ok(new { Error = ex.Message });
            }
        }

        // Makes count downstream GET requests to this app's own Echo endpoint
        [HttpGet("DownstreamMultipleToSelf")]
        public async Task<IActionResult> DownstreamMultipleToSelf(int count)
        {
            var echoUrl = $"{Request.Scheme}://{Request.Host}/Rasp/Echo";
            int successCount = 0;
            int failureCount = 0;

            for (int i = 0; i < count; i++)
            {
                try
                {
                    var response = await _httpClient.GetAsync(echoUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        successCount++;
                    }
                    else
                    {
                        failureCount++;
                    }
                }
                catch
                {
                    failureCount++;
                }
            }

            return Ok(new { TotalRequests = count, Successful = successCount, Failed = failureCount });
        }

        // Makes a downstream POST with a large generated body to this app's own Echo endpoint
        [HttpGet("DownstreamLargeBodyToSelf")]
        public async Task<IActionResult> DownstreamLargeBodyToSelf(int sizeBytes)
        {
            var echoUrl = $"{Request.Scheme}://{Request.Host}/Rasp/Echo";
            try
            {
                var sb = new StringBuilder();
                sb.Append("{\"data\":\"");
                sb.Append(new string('a', Math.Max(0, sizeBytes - 12)));
                sb.Append("\"}");

                var content = new StringContent(sb.ToString(), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(echoUrl, content);
                var responseBody = await response.Content.ReadAsStringAsync();
                return Ok(new { StatusCode = (int)response.StatusCode, BodySize = sizeBytes });
            }
            catch (Exception ex)
            {
                return Ok(new { Error = ex.Message });
            }
        }
    }
}
