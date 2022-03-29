using System;
using System.Net.Http;

namespace TinyGet.Config
{
    internal interface IAppArguments
    {
        int Loop { get; }

        int Threads { get; }

        String Srv { get; }

        String Uri { get; }

        int Port { get; }

        int Status { get; }

        HttpMethod Method { get; }

        bool IsInfinite { get; }

        string GetUrl();
    }
}