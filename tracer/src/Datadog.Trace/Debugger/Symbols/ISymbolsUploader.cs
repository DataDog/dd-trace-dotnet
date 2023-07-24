// <copyright file="ISymbolsUploader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;

namespace Datadog.Trace.Debugger.Symbols
{
    internal interface ISymbolsUploader : IDisposable
    {
        Task StartExtractingAssemblySymbolsAsync();
    }

    internal class NoOpUploader : ISymbolsUploader
    {
        public void Dispose()
        {
        }

        public async Task StartExtractingAssemblySymbolsAsync()
        {
            await Task.CompletedTask.ConfigureAwait(false);
        }
    }
}
