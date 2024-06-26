﻿// <copyright company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
// <auto-generated/>

#nullable enable

using Datadog.Trace.Processors;
using Datadog.Trace.Tagging;
using System;

namespace Datadog.Trace.Iast
{
    partial class IastTags
    {
        // IastJsonBytes = MessagePack.Serialize("_dd.iast.json");
        private static ReadOnlySpan<byte> IastJsonBytes => new byte[] { 173, 95, 100, 100, 46, 105, 97, 115, 116, 46, 106, 115, 111, 110 };
        // IastJsonTagSizeExceededBytes = MessagePack.Serialize("_dd.iast.json.tag.size.exceeded");
        private static ReadOnlySpan<byte> IastJsonTagSizeExceededBytes => new byte[] { 191, 95, 100, 100, 46, 105, 97, 115, 116, 46, 106, 115, 111, 110, 46, 116, 97, 103, 46, 115, 105, 122, 101, 46, 101, 120, 99, 101, 101, 100, 101, 100 };
        // IastEnabledBytes = MessagePack.Serialize("_dd.iast.enabled");
        private static ReadOnlySpan<byte> IastEnabledBytes => new byte[] { 176, 95, 100, 100, 46, 105, 97, 115, 116, 46, 101, 110, 97, 98, 108, 101, 100 };

        public override string? GetTag(string key)
        {
            return key switch
            {
                "_dd.iast.json" => IastJson,
                "_dd.iast.json.tag.size.exceeded" => IastJsonTagSizeExceeded,
                "_dd.iast.enabled" => IastEnabled,
                _ => base.GetTag(key),
            };
        }

        public override void SetTag(string key, string value)
        {
            switch(key)
            {
                case "_dd.iast.json": 
                    IastJson = value;
                    break;
                case "_dd.iast.json.tag.size.exceeded": 
                    IastJsonTagSizeExceeded = value;
                    break;
                case "_dd.iast.enabled": 
                    IastEnabled = value;
                    break;
                default: 
                    base.SetTag(key, value);
                    break;
            }
        }

        public override void EnumerateTags<TProcessor>(ref TProcessor processor)
        {
            if (IastJson is not null)
            {
                processor.Process(new TagItem<string>("_dd.iast.json", IastJson, IastJsonBytes));
            }

            if (IastJsonTagSizeExceeded is not null)
            {
                processor.Process(new TagItem<string>("_dd.iast.json.tag.size.exceeded", IastJsonTagSizeExceeded, IastJsonTagSizeExceededBytes));
            }

            if (IastEnabled is not null)
            {
                processor.Process(new TagItem<string>("_dd.iast.enabled", IastEnabled, IastEnabledBytes));
            }

            base.EnumerateTags(ref processor);
        }

        protected override void WriteAdditionalTags(System.Text.StringBuilder sb)
        {
            if (IastJson is not null)
            {
                sb.Append("_dd.iast.json (tag):")
                  .Append(IastJson)
                  .Append(',');
            }

            if (IastJsonTagSizeExceeded is not null)
            {
                sb.Append("_dd.iast.json.tag.size.exceeded (tag):")
                  .Append(IastJsonTagSizeExceeded)
                  .Append(',');
            }

            if (IastEnabled is not null)
            {
                sb.Append("_dd.iast.enabled (tag):")
                  .Append(IastEnabled)
                  .Append(',');
            }

            base.WriteAdditionalTags(sb);
        }
    }
}
