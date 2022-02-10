using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace TinyGet.Requests
{
    internal class RequestSender : IRequestSender
    {
        private readonly Context _context;
        private Stopwatch _swRateLimiter;
        private static TimeSpan _exceptionRateLimit = TimeSpan.FromMinutes(10);
        private long _numberOfExceptions = 0;

        public RequestSender(Context context)
        {
            _context = context;
            _swRateLimiter = new Stopwatch();
        }

        public async Task Run()
        {
            using (HttpClient client = new HttpClient())
            {
                _swRateLimiter.Start();

                if (_context.Arguments.IsInfinite)
                {
                    Console.WriteLine("Sending requests infinitely");
                    while (!_context.Token.IsCancellationRequested)
                    {
                        await SendRequest(client);
                    }
                }
                else
                {
                    Console.WriteLine("Sending requests for " + _context.Arguments.Loop + " iterations");
                    for (int i = 0; i < _context.Arguments.Loop; i++)
                    {
                        await SendRequest(client);
                    }
                }
            }
        }

        private async Task SendRequest(HttpClient client)
        {
            try
            {
                HttpRequestMessage request = new HttpRequestMessage(_context.Arguments.Method, _context.Arguments.GetUrl());
                HttpResponseMessage result = await client.SendAsync(request, _context.Token);

                if (!_context.Arguments.IsInfinite && (int)result.StatusCode != _context.Arguments.Status)
                {
                    throw new ApplicationException("Status code is not equal to " + _context.Arguments.Status);
                }
            }
            catch(Exception e)
            {
                ++_numberOfExceptions;
                if (_swRateLimiter.Elapsed > _exceptionRateLimit)
                {
                    Console.WriteLine(e.Message);
                    Console.WriteLine($"Number of exceptions since last time {_numberOfExceptions}");
                    _numberOfExceptions = 0;
                    _swRateLimiter.Restart();
                }
            }
        }
    }
}
