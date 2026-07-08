// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        var recordingMode = GetArg(args, "--recording") ?? "manual";
        var rootMode = GetArg(args, "--root-mode") ?? "logical";
        var recordManually = !string.Equals(recordingMode, "native", StringComparison.OrdinalIgnoreCase);
        var outputPath = GetArg(args, "--output") ??
                         Path.Combine(Path.GetTempPath(), "datadog-live-debugger-poc", "flow-events.dflp");
        DeleteIfExists(outputPath);
        DeleteIfExists(outputPath + ".methods");
        DeleteIfExists(Path.ChangeExtension(outputPath, ".warmup.dflp"));
        DeleteIfExists(Path.ChangeExtension(outputPath, ".warmup.dflp") + ".methods");

        if (recordManually)
        {
            RegisterManualMethodNames();
        }

        using (var scenarioScope = Tracer.Instance.StartActive("live-debugger-poc." + scenario))
        {
            scenarioScope.Span.ResourceName = "scenario " + scenario;
            scenarioScope.Span.SetTag("live-debugger-poc.scenario", scenario);
            using var recorderOperation = string.Equals(scenario, "benchmark", StringComparison.OrdinalIgnoreCase)
                                              ? null
                                              : StartRecorderRoot(rootMode, scenario);

            try
            {
                await RunScenario(scenario, recordManually, args, outputPath);
            }
            catch (Exception ex)
            {
                scenarioScope.Span.SetException(ex);
                System.Console.WriteLine("Scenario threw: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        await Tracer.Instance.FlushAsync();
        var postRunDelayMs = GetIntArg(args, "--post-run-delay-ms", 0);
        if (postRunDelayMs > 0)
        {
            await Task.Delay(postRunDelayMs);
        }

        await Task.Yield();
        var flushStart = Stopwatch.GetTimestamp();
        var count = File.Exists(outputPath) ? ReadEventCount(outputPath) : FlowRecorder.Flush(outputPath);
        var flushElapsedMs = ToMilliseconds(Stopwatch.GetTimestamp() - flushStart);
        System.Console.WriteLine("Wrote " + count + " flow events to " + outputPath);
        System.Console.WriteLine("Dropped events: " + FlowRecorder.DroppedEvents);
        System.Console.WriteLine("FlushElapsedMs: " + Format(flushElapsedMs));
        return 0;
    }

    private static Task RunScenario(string scenario, bool recordManually, string[] args, string outputPath)
    {
        return scenario switch
        {
            "async" => AsyncOnlyScenario(recordManually),
            "benchmark" => BenchmarkCheckoutLoopAsync(args, recordManually, outputPath),
            "multi-span" => MultiSpanCheckoutAsync(recordManually),
            "presentation" => PresentationCheckoutAsync(recordManually),
            "slow" => CheckoutAsync(delayMilliseconds: 75, throwAtPayment: false, recordManually),
            "exception" => CheckoutAsync(delayMilliseconds: 5, throwAtPayment: true, recordManually),
            _ => CheckoutAsync(delayMilliseconds: 5, throwAtPayment: false, recordManually)
        };
    }

    private static async Task BenchmarkCheckoutLoopAsync(string[] args, bool recordManually, string outputPath)
    {
        var iterations = GetIntArg(args, "--iterations", 100_000);
        var warmup = GetIntArg(args, "--warmup", 5_000);
        var exceptionEvery = GetIntArg(args, "--exception-every", 0);
        var probeInstallDelayMs = GetIntArg(args, "--probe-install-delay-ms", 0);
        var durations = new long[iterations];
        var errors = 0;

        if (probeInstallDelayMs > 0)
        {
            await Task.Delay(probeInstallDelayMs);
        }

        for (var i = 0; i < warmup; i++)
        {
            using (StartBenchmarkRecorderRoot("warmup"))
            {
                try
                {
                    await BenchmarkCheckoutRequestAsync(i, exceptionEvery, recordManually);
                }
                catch (InvalidOperationException)
                {
                    // Expected when the benchmark intentionally injects sparse failures.
                }
            }
        }

        var warmupEvents = FlowRecorder.Flush(Path.ChangeExtension(outputPath, ".warmup.dflp"));
        System.Console.WriteLine("WarmupEvents: " + warmupEvents);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var gen0Before = GC.CollectionCount(0);
        var gen1Before = GC.CollectionCount(1);
        var gen2Before = GC.CollectionCount(2);
        var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        var start = Stopwatch.GetTimestamp();

        for (var i = 0; i < iterations; i++)
        {
            using (StartBenchmarkRecorderRoot("measured"))
            {
                var iterationStart = Stopwatch.GetTimestamp();
                try
                {
                    await BenchmarkCheckoutRequestAsync(i, exceptionEvery, recordManually);
                }
                catch (InvalidOperationException)
                {
                    errors++;
                }

                durations[i] = Stopwatch.GetTimestamp() - iterationStart;
            }
        }

        var elapsed = Stopwatch.GetTimestamp() - start;
        var allocatedAfter = GC.GetTotalAllocatedBytes(precise: true);
        var gen0 = GC.CollectionCount(0) - gen0Before;
        var gen1 = GC.CollectionCount(1) - gen1Before;
        var gen2 = GC.CollectionCount(2) - gen2Before;

        Array.Sort(durations);
        var elapsedMs = ToMilliseconds(elapsed);
        var throughput = iterations / (elapsedMs / 1000.0);
        var allocatedBytes = allocatedAfter - allocatedBefore;

        System.Console.WriteLine("Benchmark results");
        System.Console.WriteLine("Iterations: " + iterations);
        System.Console.WriteLine("Warmup: " + warmup);
        System.Console.WriteLine("Errors: " + errors);
        System.Console.WriteLine("ElapsedMs: " + Format(elapsedMs));
        System.Console.WriteLine("ThroughputPerSecond: " + Format(throughput));
        System.Console.WriteLine("P50Us: " + Format(ToMicroseconds(Percentile(durations, 0.50))));
        System.Console.WriteLine("P95Us: " + Format(ToMicroseconds(Percentile(durations, 0.95))));
        System.Console.WriteLine("P99Us: " + Format(ToMicroseconds(Percentile(durations, 0.99))));
        System.Console.WriteLine("AllocatedBytes: " + allocatedBytes);
        System.Console.WriteLine("AllocatedBytesPerRequest: " + Format(allocatedBytes / (double)iterations));
        System.Console.WriteLine("Gen0Collections: " + gen0);
        System.Console.WriteLine("Gen1Collections: " + gen1);
        System.Console.WriteLine("Gen2Collections: " + gen2);
    }

    private static async Task<string> BenchmarkCheckoutRequestAsync(int iteration, int exceptionEvery, bool recordManually)
    {
        long operationId = 0;
        long generation = 0;
        var state = EnterManualAsync(recordManually, methodMetadataIndex: 130, parentOperationId: 0, ref operationId, ref generation);
        Exception? exception = null;
        try
        {
            var cart = BuildBenchmarkCart(iteration, recordManually);
            var subtotal = BenchmarkCalculateSubtotal(cart, recordManually);
            var discount = await BenchmarkDiscountAsync(subtotal, iteration, operationId, recordManually);
            var shipping = await BenchmarkShippingAsync(iteration, operationId, recordManually);
            var authorization = BenchmarkAuthorize(subtotal - discount + shipping, cart.CustomerId, recordManually);
            await BenchmarkReceiptAsync(authorization, operationId, recordManually);

            if (exceptionEvery > 0 && iteration % exceptionEvery == 0)
            {
                throw new InvalidOperationException("Benchmark injected failure.");
            }

            return authorization;
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

    private static DemoCart BuildBenchmarkCart(int iteration, bool recordManually)
    {
        var state = EnterManual(recordManually, methodMetadataIndex: 131);
        try
        {
            var items = new List<string>(3)
            {
                "sku-" + (iteration % 7),
                "sku-" + (iteration % 11),
                "sku-" + (iteration % 13)
            };
            var lineItems = new List<DemoLineItem>(3)
            {
                new(items[0], 1, 12.50m),
                new(items[1], 1, 13.50m),
                new(items[2], 1, 14.50m)
            };
            return new DemoCart("cart-" + iteration, "customer-" + (iteration % 100), items, lineItems);
        }
        finally
        {
            FlowRecorder.Exit(ref state);
        }
    }

    private static decimal BenchmarkCalculateSubtotal(DemoCart cart, bool recordManually)
    {
        var state = EnterManual(recordManually, methodMetadataIndex: 132);
        try
        {
            var subtotal = 0m;
            for (var i = 0; i < cart.Items.Count; i++)
            {
                subtotal += 12.50m + (cart.Items[i].Length % 5);
            }

            return subtotal;
        }
        finally
        {
            FlowRecorder.Exit(ref state);
        }
    }

    private static async Task<decimal> BenchmarkDiscountAsync(decimal subtotal, int iteration, long parentOperationId, bool recordManually)
    {
        long operationId = 0;
        long generation = 0;
        var state = EnterManualAsync(recordManually, methodMetadataIndex: 133, parentOperationId, ref operationId, ref generation);
        try
        {
            await Task.Yield();
            return iteration % 3 == 0 ? decimal.Round(subtotal * 0.10m, 2) : 0m;
        }
        finally
        {
            FlowRecorder.Exit(ref state);
        }
    }

    private static async Task<decimal> BenchmarkShippingAsync(int iteration, long parentOperationId, bool recordManually)
    {
        long operationId = 0;
        long generation = 0;
        var state = EnterManualAsync(recordManually, methodMetadataIndex: 134, parentOperationId, ref operationId, ref generation);
        try
        {
            await Task.CompletedTask;
            return iteration % 5 == 0 ? 0m : 4.99m;
        }
        finally
        {
            FlowRecorder.Exit(ref state);
        }
    }

    private static string BenchmarkAuthorize(decimal amount, string customerId, bool recordManually)
    {
        var state = EnterManual(recordManually, methodMetadataIndex: 135);
        try
        {
            var checksum = amount.GetHashCode() ^ customerId.GetHashCode();
            return "auth-" + checksum.ToString("x");
        }
        finally
        {
            FlowRecorder.Exit(ref state);
        }
    }

    private static async Task BenchmarkReceiptAsync(string authorization, long parentOperationId, bool recordManually)
    {
        long operationId = 0;
        long generation = 0;
        var state = EnterManualAsync(recordManually, methodMetadataIndex: 136, parentOperationId, ref operationId, ref generation);
        try
        {
            await Task.Yield();
            _ = authorization.ToUpperInvariant();
        }
        finally
        {
            FlowRecorder.Exit(ref state);
        }
    }

    private static async Task PresentationCheckoutAsync(bool recordManually)
    {
        long operationId = 0;
        long generation = 0;
        var state = EnterManualAsync(recordManually, methodMetadataIndex: 110, parentOperationId: 0, ref operationId, ref generation);
        try
        {
            var cart = BuildDemoCart(recordManually);
            var eligibility = await ValidateCustomerEligibilityAsync(cart, operationId, recordManually);
            var quote = await BuildCheckoutQuoteAsync(cart, eligibility, operationId, recordManually);
            var reservation = await ReserveInventoryAsync(quote, operationId, recordManually);
            var receipt = await CapturePaymentAsync(quote, reservation, operationId, recordManually);
            await SendReceiptAsync(receipt, operationId, recordManually);
            System.Console.WriteLine("Presentation checkout completed: " + receipt);
        }
        finally
        {
            FlowRecorder.Exit(ref state);
        }
    }

    private static async Task MultiSpanCheckoutAsync(bool recordManually)
    {
        long operationId = 0;
        long generation = 0;
        var state = EnterManualAsync(recordManually, methodMetadataIndex: 125, parentOperationId: 0, ref operationId, ref generation);
        try
        {
            using (StartSampleScope("checkout.request", "POST /demo/checkout", "request"))
            {
                var cart = BuildDemoCart(recordManually);

                CustomerEligibility eligibility;
                using (StartSampleScope("checkout.eligibility", "validate customer eligibility", "eligibility"))
                {
                    eligibility = await ValidateCustomerEligibilityAsync(cart, operationId, recordManually);
                }

                CheckoutQuote quote;
                using (StartSampleScope("checkout.pricing", "build checkout quote", "pricing"))
                {
                    quote = await BuildCheckoutQuoteAsync(cart, eligibility, operationId, recordManually);
                }

                InventoryReservation reservation;
                using (StartSampleScope("checkout.inventory", "reserve inventory", "inventory"))
                {
                    reservation = await ReserveInventoryAsync(quote, operationId, recordManually);
                }

                string receipt;
                using (StartSampleScope("checkout.payment", "capture payment", "payment"))
                {
                    receipt = await CapturePaymentAsync(quote, reservation, operationId, recordManually);
                }

                using (StartSampleScope("checkout.receipt", "send receipt", "receipt"))
                {
                    await SendReceiptAsync(receipt, operationId, recordManually);
                }

                System.Console.WriteLine("Multi-span checkout completed: " + receipt);
            }
        }
        finally
        {
            FlowRecorder.Exit(ref state);
        }
    }

    private static async Task CheckoutAsync(int delayMilliseconds, bool throwAtPayment, bool recordManually)
    {
        long operationId = 0;
        long generation = 0;
        var state = EnterManualAsync(recordManually, methodMetadataIndex: 100, parentOperationId: 0, ref operationId, ref generation);
        Exception? exception = null;
        try
        {
            var cart = BuildDemoCart(recordManually);
            ValidateCart(cart, recordManually);
            await PriceCartAsync(delayMilliseconds, recordManually);
            ChargePayment(throwAtPayment, recordManually);
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

    private static void ValidateCart(DemoCart cart, bool recordManually)
    {
        var state = EnterManual(recordManually, methodMetadataIndex: 101);
        try
        {
            _ = DateTime.UtcNow.DayOfWeek;
            if (cart.LineItems.Count == 0)
            {
                throw new InvalidOperationException("The cart must have at least one item.");
            }
        }
        finally
        {
            FlowRecorder.Exit(ref state);
        }
    }

    private static async Task PriceCartAsync(int delayMilliseconds, bool recordManually)
    {
        var state = EnterManual(recordManually, methodMetadataIndex: 102);
        try
        {
            await Task.Delay(delayMilliseconds);
            ApplyDiscount(recordManually);
        }
        finally
        {
            FlowRecorder.Exit(ref state);
        }
    }

    private static void ApplyDiscount(bool recordManually)
    {
        var state = EnterManual(recordManually, methodMetadataIndex: 103);
        try
        {
            _ = Math.Sqrt(144);
        }
        finally
        {
            FlowRecorder.Exit(ref state);
        }
    }

    private static void ChargePayment(bool shouldThrow, bool recordManually)
    {
        var state = EnterManual(recordManually, methodMetadataIndex: 104);
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

    private static async Task AsyncOnlyScenario(bool recordManually)
    {
        var value = await AsyncValueAsync(parentOperationId: 0, recordManually);
        await AsyncLeafAsync(value, parentOperationId: 0, recordManually);
        System.Console.WriteLine("Async scenario result: " + value);
    }

    private static async Task<int> AsyncValueAsync(long parentOperationId, bool recordManually)
    {
        long operationId = 0;
        long generation = 0;
        var state = EnterManualAsync(recordManually, methodMetadataIndex: 105, parentOperationId, ref operationId, ref generation);
        try
        {
            await Task.Yield();
            return 42;
        }
        finally
        {
            FlowRecorder.Exit(ref state);
        }
    }

    private static DemoCart BuildDemoCart(bool recordManually)
    {
        var state = EnterManual(recordManually, methodMetadataIndex: 111);
        try
        {
            var items = new List<string> { "debugger-hoodie", "trace-stickers", "async-map-poster" };
            var lineItems = new List<DemoLineItem>
            {
                new("debugger-hoodie", 1, 39.90m),
                new("trace-stickers", 4, 2.50m),
                new("async-map-poster", 1, 12.00m),
                new("profiler-mug", 2, 9.95m),
                new("runtime-pins", 5, 1.25m),
                new("calltarget-socks", 1, 7.75m),
                new("snapshot-notebook", 3, 4.50m),
                new("flow-recorder-tote", 1, 14.25m)
            };
            return new DemoCart("cart-live-debugger-poc", "customer-42", items, lineItems);
        }
        finally
        {
            FlowRecorder.Exit(ref state);
        }
    }

    private static async Task<CustomerEligibility> ValidateCustomerEligibilityAsync(DemoCart cart, long parentOperationId, bool recordManually)
    {
        long operationId = 0;
        long generation = 0;
        var state = EnterManualAsync(recordManually, methodMetadataIndex: 112, parentOperationId, ref operationId, ref generation);
        try
        {
            ValidateCartShape(cart, recordManually);
            var loyaltyTier = await LoadCustomerLoyaltyTierAsync(cart.CustomerId, operationId, recordManually);
            await CheckFraudSignalsAsync(cart, operationId, recordManually);
            return new CustomerEligibility(loyaltyTier, ExpressShipping: loyaltyTier == "gold");
        }
        finally
        {
            FlowRecorder.Exit(ref state);
        }
    }

    private static void ValidateCartShape(DemoCart cart, bool recordManually)
    {
        var state = EnterManual(recordManually, methodMetadataIndex: 113);
        try
        {
            if (cart.LineItems.Count == 0)
            {
                throw new InvalidOperationException("The presentation cart must have at least one item.");
            }
        }
        finally
        {
            FlowRecorder.Exit(ref state);
        }
    }

    private static async Task<string> LoadCustomerLoyaltyTierAsync(string customerId, long parentOperationId, bool recordManually)
    {
        long operationId = 0;
        long generation = 0;
        var state = EnterManualAsync(recordManually, methodMetadataIndex: 114, parentOperationId, ref operationId, ref generation);
        try
        {
            await Task.Delay(8);
            _ = customerId.Length;
            return "gold";
        }
        finally
        {
            FlowRecorder.Exit(ref state);
        }
    }

    private static async Task CheckFraudSignalsAsync(DemoCart cart, long parentOperationId, bool recordManually)
    {
        long operationId = 0;
        long generation = 0;
        var state = EnterManualAsync(recordManually, methodMetadataIndex: 115, parentOperationId, ref operationId, ref generation);
        try
        {
            await Task.Yield();
            _ = cart.CartId.GetHashCode();
        }
        finally
        {
            FlowRecorder.Exit(ref state);
        }
    }

    private static async Task<CheckoutQuote> BuildCheckoutQuoteAsync(DemoCart cart, CustomerEligibility eligibility, long parentOperationId, bool recordManually)
    {
        long operationId = 0;
        long generation = 0;
        var state = EnterManualAsync(recordManually, methodMetadataIndex: 116, parentOperationId, ref operationId, ref generation);
        try
        {
            var subtotal = CalculateSubtotal(cart, recordManually);
            var discount = await CalculatePersonalizedDiscountAsync(subtotal, eligibility, operationId, recordManually);
            var shipping = await CalculateShippingAsync(eligibility, operationId, recordManually);
            return new CheckoutQuote(cart.CartId, subtotal, discount, shipping);
        }
        finally
        {
            FlowRecorder.Exit(ref state);
        }
    }

    private static decimal CalculateSubtotal(DemoCart cart, bool recordManually)
    {
        var state = EnterManual(recordManually, methodMetadataIndex: 117);
        try
        {
            var subtotal = 0m;
            for (var i = 0; i < cart.LineItems.Count; i++)
            {
                var item = cart.LineItems[i];
                subtotal += item.UnitPrice * item.Quantity;
            }

            return subtotal;
        }
        finally
        {
            FlowRecorder.Exit(ref state);
        }
    }

    private static async Task<decimal> CalculatePersonalizedDiscountAsync(decimal subtotal, CustomerEligibility eligibility, long parentOperationId, bool recordManually)
    {
        long operationId = 0;
        long generation = 0;
        var state = EnterManualAsync(recordManually, methodMetadataIndex: 118, parentOperationId, ref operationId, ref generation);
        try
        {
            await Task.Delay(6);
            return eligibility.LoyaltyTier == "gold" ? decimal.Round(subtotal * 0.15m, 2) : 0m;
        }
        finally
        {
            FlowRecorder.Exit(ref state);
        }
    }

    private static async Task<decimal> CalculateShippingAsync(CustomerEligibility eligibility, long parentOperationId, bool recordManually)
    {
        long operationId = 0;
        long generation = 0;
        var state = EnterManualAsync(recordManually, methodMetadataIndex: 119, parentOperationId, ref operationId, ref generation);
        try
        {
            await Task.Yield();
            return eligibility.ExpressShipping ? 4.99m : 9.99m;
        }
        finally
        {
            FlowRecorder.Exit(ref state);
        }
    }

    private static async Task<InventoryReservation> ReserveInventoryAsync(CheckoutQuote quote, long parentOperationId, bool recordManually)
    {
        long operationId = 0;
        long generation = 0;
        var state = EnterManualAsync(recordManually, methodMetadataIndex: 120, parentOperationId, ref operationId, ref generation);
        try
        {
            await Task.Delay(5);
            return new InventoryReservation("reservation-" + quote.CartId, ExpiresInSeconds: 90);
        }
        finally
        {
            FlowRecorder.Exit(ref state);
        }
    }

    private static async Task<string> CapturePaymentAsync(CheckoutQuote quote, InventoryReservation reservation, long parentOperationId, bool recordManually)
    {
        long operationId = 0;
        long generation = 0;
        var state = EnterManualAsync(recordManually, methodMetadataIndex: 121, parentOperationId, ref operationId, ref generation);
        try
        {
            var authorization = await AuthorizeCardAsync(quote.Total, operationId, recordManually);
            ConfirmReservation(reservation, authorization, recordManually);
            return "receipt-" + quote.CartId + "-" + authorization;
        }
        finally
        {
            FlowRecorder.Exit(ref state);
        }
    }

    private static async Task<string> AuthorizeCardAsync(decimal amount, long parentOperationId, bool recordManually)
    {
        long operationId = 0;
        long generation = 0;
        var state = EnterManualAsync(recordManually, methodMetadataIndex: 122, parentOperationId, ref operationId, ref generation);
        try
        {
            await Task.Delay(10);
            return "auth-" + amount.ToString("0.00");
        }
        finally
        {
            FlowRecorder.Exit(ref state);
        }
    }

    private static void ConfirmReservation(InventoryReservation reservation, string authorization, bool recordManually)
    {
        var state = EnterManual(recordManually, methodMetadataIndex: 123);
        try
        {
            _ = reservation.ReservationId.Length + authorization.Length;
        }
        finally
        {
            FlowRecorder.Exit(ref state);
        }
    }

    private static async Task SendReceiptAsync(string receipt, long parentOperationId, bool recordManually)
    {
        long operationId = 0;
        long generation = 0;
        var state = EnterManualAsync(recordManually, methodMetadataIndex: 124, parentOperationId, ref operationId, ref generation);
        try
        {
            await Task.Delay(4);
            _ = receipt.ToUpperInvariant();
        }
        finally
        {
            FlowRecorder.Exit(ref state);
        }
    }

    private static async Task AsyncLeafAsync(int value, long parentOperationId, bool recordManually)
    {
        long operationId = 0;
        long generation = 0;
        var state = EnterManualAsync(recordManually, methodMetadataIndex: 106, parentOperationId, ref operationId, ref generation);
        try
        {
            await Task.Delay(1);
            _ = value.ToString();
        }
        finally
        {
            FlowRecorder.Exit(ref state);
        }
    }

    private static FlowRecorderState EnterManual(bool recordManually, int methodMetadataIndex)
    {
        return recordManually ? FlowRecorder.Enter(methodMetadataIndex) : default;
    }

    private static FlowRecorderState EnterManualAsync(bool recordManually, int methodMetadataIndex, long parentOperationId, ref long operationId, ref long generation)
    {
        return recordManually ? FlowRecorder.EnterAsyncOperationForTesting(methodMetadataIndex, parentOperationId, ref operationId, ref generation) : default;
    }

    private static IScope StartSampleScope(string operationName, string resourceName, string phase)
    {
        var scope = Tracer.Instance.StartActive("live-debugger-poc." + operationName);
        scope.Span.ResourceName = resourceName;
        scope.Span.Type = "custom";
        scope.Span.SetTag("live-debugger-poc.phase", phase);
        return scope;
    }

    private static IDisposable? StartRecorderRoot(string rootMode, string scenario)
    {
        if (string.Equals(rootMode, "none", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var root = "Samples.LiveDebuggerPoc.Console.Program." + scenario;
        if (string.Equals(rootMode, "traced", StringComparison.OrdinalIgnoreCase))
        {
            return FlowRecorder.StartConfiguredOperation("traced-sample-root", root);
        }

        return FlowRecorder.StartConfiguredOperation("logical-sample-root", root);
    }

    private static IDisposable? StartBenchmarkRecorderRoot(string phase)
    {
        return IsFlowRecorderEnabled()
                   ? FlowRecorder.StartConfiguredOperation("benchmark-" + phase, "Samples.LiveDebuggerPoc.Console.Program.BenchmarkCheckoutRequestAsync")
                   : null;
    }

    private static bool IsFlowRecorderEnabled()
    {
        var value = Environment.GetEnvironmentVariable("DD_INTERNAL_DEBUGGER_FLOW_RECORDER_ENABLED");
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static int ReadEventCount(string path)
    {
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new BinaryReader(stream);
        _ = reader.ReadInt32();
        _ = reader.ReadInt32();
        return reader.ReadInt32();
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void RegisterManualMethodNames()
    {
        FlowRecorder.RegisterMethodNameForTesting(100, "Samples.LiveDebuggerPoc.Console.Program.CheckoutAsync");
        FlowRecorder.RegisterMethodNameForTesting(101, "Samples.LiveDebuggerPoc.Console.Program.ValidateCart");
        FlowRecorder.RegisterMethodNameForTesting(102, "Samples.LiveDebuggerPoc.Console.Program.PriceCartAsync");
        FlowRecorder.RegisterMethodNameForTesting(103, "Samples.LiveDebuggerPoc.Console.Program.ApplyDiscount");
        FlowRecorder.RegisterMethodNameForTesting(104, "Samples.LiveDebuggerPoc.Console.Program.ChargePayment");
        FlowRecorder.RegisterMethodNameForTesting(105, "Samples.LiveDebuggerPoc.Console.Program.AsyncValueAsync");
        FlowRecorder.RegisterMethodNameForTesting(106, "Samples.LiveDebuggerPoc.Console.Program.AsyncLeafAsync");
        FlowRecorder.RegisterMethodNameForTesting(110, "Samples.LiveDebuggerPoc.Console.Program.PresentationCheckoutAsync");
        FlowRecorder.RegisterMethodNameForTesting(111, "Samples.LiveDebuggerPoc.Console.Program.BuildDemoCart");
        FlowRecorder.RegisterMethodNameForTesting(112, "Samples.LiveDebuggerPoc.Console.Program.ValidateCustomerEligibilityAsync");
        FlowRecorder.RegisterMethodNameForTesting(113, "Samples.LiveDebuggerPoc.Console.Program.ValidateCartShape");
        FlowRecorder.RegisterMethodNameForTesting(114, "Samples.LiveDebuggerPoc.Console.Program.LoadCustomerLoyaltyTierAsync");
        FlowRecorder.RegisterMethodNameForTesting(115, "Samples.LiveDebuggerPoc.Console.Program.CheckFraudSignalsAsync");
        FlowRecorder.RegisterMethodNameForTesting(116, "Samples.LiveDebuggerPoc.Console.Program.BuildCheckoutQuoteAsync");
        FlowRecorder.RegisterMethodNameForTesting(117, "Samples.LiveDebuggerPoc.Console.Program.CalculateSubtotal");
        FlowRecorder.RegisterMethodNameForTesting(118, "Samples.LiveDebuggerPoc.Console.Program.CalculatePersonalizedDiscountAsync");
        FlowRecorder.RegisterMethodNameForTesting(119, "Samples.LiveDebuggerPoc.Console.Program.CalculateShippingAsync");
        FlowRecorder.RegisterMethodNameForTesting(120, "Samples.LiveDebuggerPoc.Console.Program.ReserveInventoryAsync");
        FlowRecorder.RegisterMethodNameForTesting(121, "Samples.LiveDebuggerPoc.Console.Program.CapturePaymentAsync");
        FlowRecorder.RegisterMethodNameForTesting(122, "Samples.LiveDebuggerPoc.Console.Program.AuthorizeCardAsync");
        FlowRecorder.RegisterMethodNameForTesting(123, "Samples.LiveDebuggerPoc.Console.Program.ConfirmReservation");
        FlowRecorder.RegisterMethodNameForTesting(124, "Samples.LiveDebuggerPoc.Console.Program.SendReceiptAsync");
        FlowRecorder.RegisterMethodNameForTesting(125, "Samples.LiveDebuggerPoc.Console.Program.MultiSpanCheckoutAsync");
        FlowRecorder.RegisterMethodNameForTesting(130, "Samples.LiveDebuggerPoc.Console.Program.BenchmarkCheckoutRequestAsync");
        FlowRecorder.RegisterMethodNameForTesting(131, "Samples.LiveDebuggerPoc.Console.Program.BuildBenchmarkCart");
        FlowRecorder.RegisterMethodNameForTesting(132, "Samples.LiveDebuggerPoc.Console.Program.BenchmarkCalculateSubtotal");
        FlowRecorder.RegisterMethodNameForTesting(133, "Samples.LiveDebuggerPoc.Console.Program.BenchmarkDiscountAsync");
        FlowRecorder.RegisterMethodNameForTesting(134, "Samples.LiveDebuggerPoc.Console.Program.BenchmarkShippingAsync");
        FlowRecorder.RegisterMethodNameForTesting(135, "Samples.LiveDebuggerPoc.Console.Program.BenchmarkAuthorize");
        FlowRecorder.RegisterMethodNameForTesting(136, "Samples.LiveDebuggerPoc.Console.Program.BenchmarkReceiptAsync");
    }

    private static string? GetArg(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static int GetIntArg(string[] args, string name, int defaultValue)
    {
        var value = GetArg(args, name);
        return int.TryParse(value, out var parsed) && parsed >= 0 ? parsed : defaultValue;
    }

    private static long Percentile(long[] sortedDurations, double percentile)
    {
        if (sortedDurations.Length == 0)
        {
            return 0;
        }

        var index = (int)Math.Ceiling(percentile * sortedDurations.Length) - 1;
        index = Math.Max(0, Math.Min(sortedDurations.Length - 1, index));
        return sortedDurations[index];
    }

    private static double ToMilliseconds(long elapsedTimestamp)
    {
        return elapsedTimestamp * 1000.0 / Stopwatch.Frequency;
    }

    private static double ToMicroseconds(long elapsedTimestamp)
    {
        return elapsedTimestamp * 1_000_000.0 / Stopwatch.Frequency;
    }

    private static string Format(double value)
    {
        return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed record DemoCart(string CartId, string CustomerId, List<string> Items, List<DemoLineItem> LineItems);

    private sealed record DemoLineItem(string Sku, int Quantity, decimal UnitPrice);

    private readonly record struct CustomerEligibility(string LoyaltyTier, bool ExpressShipping);

    private readonly record struct CheckoutQuote(string CartId, decimal Subtotal, decimal Discount, decimal Shipping)
    {
        public decimal Total => Subtotal - Discount + Shipping;
    }

    private readonly record struct InventoryReservation(string ReservationId, int ExpiresInSeconds);
}
