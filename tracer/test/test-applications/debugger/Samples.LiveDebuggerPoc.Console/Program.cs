// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using Datadog.Trace;
using Datadog.Trace.Debugger.LiveDebuggerPoc;

namespace Samples.LiveDebuggerPoc.Console;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        FlowRecorder.Reset();

        var scenario = GetArg(args, "--scenario") ?? "checkout";
        var outputPath = GetArg(args, "--output") ??
                         Path.Combine(Path.GetTempPath(), "datadog-live-debugger-poc", "flow-events.dflp");

        using (Tracer.Instance.StartActive("live-debugger-poc." + scenario))
        {
            try
            {
                await RunScenario(scenario);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine("Scenario threw: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        var count = FlowRecorder.Flush(outputPath);
        System.Console.WriteLine("Wrote " + count + " flow events to " + outputPath);
        System.Console.WriteLine("Dropped events: " + FlowRecorder.DroppedEvents);
        return 0;
    }

    private static Task RunScenario(string scenario)
    {
        return scenario switch
        {
            "slow" => CheckoutAsync(delayMilliseconds: 75, throwAtPayment: false),
            "exception" => CheckoutAsync(delayMilliseconds: 5, throwAtPayment: true),
            _ => CheckoutAsync(delayMilliseconds: 5, throwAtPayment: false)
        };
    }

    private static async Task CheckoutAsync(int delayMilliseconds, bool throwAtPayment)
    {
        var state = FlowRecorder.Enter(methodMetadataIndex: 100);
        Exception? exception = null;
        try
        {
            ValidateCart();
            await PriceCartAsync(delayMilliseconds);
            ChargePayment(throwAtPayment);
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            FlowRecorder.Exit(ref state, exception);
        }
    }

    private static void ValidateCart()
    {
        var state = FlowRecorder.Enter(methodMetadataIndex: 101);
        try
        {
            _ = DateTime.UtcNow.DayOfWeek;
        }
        finally
        {
            FlowRecorder.Exit(ref state);
        }
    }

    private static async Task PriceCartAsync(int delayMilliseconds)
    {
        var state = FlowRecorder.Enter(methodMetadataIndex: 102);
        try
        {
            await Task.Delay(delayMilliseconds);
            ApplyDiscount();
        }
        finally
        {
            FlowRecorder.Exit(ref state);
        }
    }

    private static void ApplyDiscount()
    {
        var state = FlowRecorder.Enter(methodMetadataIndex: 103);
        try
        {
            _ = Math.Sqrt(144);
        }
        finally
        {
            FlowRecorder.Exit(ref state);
        }
    }

    private static void ChargePayment(bool shouldThrow)
    {
        var state = FlowRecorder.Enter(methodMetadataIndex: 104);
        Exception? exception = null;
        try
        {
            if (shouldThrow)
            {
                throw new InvalidOperationException("Payment declined in POC scenario.");
            }
        }
        catch (Exception ex)
        {
            exception = ex;
            throw;
        }
        finally
        {
            FlowRecorder.Exit(ref state, exception);
        }
    }

    private static string? GetArg(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
