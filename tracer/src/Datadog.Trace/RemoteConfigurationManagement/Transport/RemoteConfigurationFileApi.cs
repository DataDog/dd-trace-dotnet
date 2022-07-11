// <copyright file="RemoteConfigurationFileApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.RemoteConfigurationManagement.Transport
{
    internal class RemoteConfigurationFileApi : IRemoteConfigurationApi
    {
        private readonly string _filePath;

        private RemoteConfigurationFileApi(string filePath)
        {
            _filePath = filePath;
        }

        public static RemoteConfigurationFileApi Create(RemoteConfigurationSettings settings)
        {
            return new RemoteConfigurationFileApi(settings.FilePath);
        }

        public Task<GetRcmResponse> GetConfigs(GetRcmRequest request)
        {
            var content = File.ReadAllText(_filePath);
            var config = JsonConvert.DeserializeObject<GetRcmResponse>(content);

            return Task.FromResult(config);
        }
    }
}
