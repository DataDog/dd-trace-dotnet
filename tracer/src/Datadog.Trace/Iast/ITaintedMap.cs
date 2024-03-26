// <copyright file = "ITaintedMap.cs" company = "Datadog" >
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace.Iast;

internal interface ITaintedMap
{
    public void Put(ITaintedObject tainted);

    public ITaintedObject Get(object obj);

    public List<ITaintedObject> GetListValues();

    public int GetEstimatedSize();
}
