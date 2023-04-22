// <copyright file="SchemaV0.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.Configuration.Schema.V0
{
    internal class SchemaV0 : ISchema
    {
        private static readonly IDatabaseSchema DatabaseSchema = new DatabaseSchemaV0();
        private static readonly IMessagingSchema MessagingSchema = new MessagingSchemaV0();

        public SchemaVersion Version => SchemaVersion.V0;

        public IDatabaseSchema Database => DatabaseSchema;

        public IMessagingSchema Messaging => MessagingSchema;
    }
}
