// <copyright file="ViewLocator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Datadog.AutoInstrumentation.Generator.ViewModels;

namespace Datadog.AutoInstrumentation.Generator
{
    internal class ViewLocator : IDataTemplate
    {
        public Control Build(object? data)
        {
            var name = data?.GetType().FullName!.Replace("ViewModel", "View");
            if (name != null)
            {
                var type = Type.GetType(name);
                if (type != null)
                {
                    return (Control)Activator.CreateInstance(type)!;
                }

                return new TextBlock { Text = "Not Found: " + name };
            }

            return null!;
        }

        public bool Match(object? data)
        {
            return data is ViewModelBase;
        }
    }
}
