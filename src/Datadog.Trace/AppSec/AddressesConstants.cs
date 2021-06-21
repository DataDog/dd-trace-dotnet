// <copyright file="AddressesConstants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.AppSec
{
    internal static class AddressesConstants
    {
        public const string RequestUriRaw = "server.request.uri.raw";
        public const string RequestMethod = "server.request.method";
        public const string RequestQuery = "server.request.query";
        public const string RequestCookies = "server.request.cookies";
        public const string RequestHeaderNoCookies = "server.request.headers.no_cookies";
        public const string RequestBody = "server.request.body";
        public const string RequestBodyFileFieldNames = "server.request.body.files_field_names";
        public const string RequestBodyFileNames = "server.request.body.filenames";
        public const string RequestPathParams = "server.request.path_params";
        public const string RequestBodyCombinedFileSize = "server.request.body.combined_file_size";
        public const string ResponseStatus = "server.response.status";

        public const string ResponseBodyRaw = "server.response.body.raw";
        public const string ResponseHeaderNoCookies = "server.response.headers.no_cookies";
    }
}
