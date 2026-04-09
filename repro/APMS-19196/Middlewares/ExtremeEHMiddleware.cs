namespace ReproApp;

/// <summary>
/// EXTREME EH middleware designed to maximize try-in-handler nesting patterns
/// that trigger the APMS-19196 EH sorting bug. Creates deeply nested async 
/// try/catch/finally structures with many exception handlers containing inner try blocks.
/// </summary>
public sealed class ExtremeEHMiddleware
{
    private readonly RequestDelegate _next;
    private static int _counter = 0;

    public ExtremeEHMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var id = Interlocked.Increment(ref _counter);
        
        try
        {
            await DeepNestedChainAsync(context, id, 0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{id}] Outer exception: {ex.Message}");
            throw; // Re-throw for next middleware
        }
    }

    private async Task DeepNestedChainAsync(HttpContext context, int id, int depth)
    {
        if (depth > 8) // Deep recursion to create complex call stacks
        {
            await _next(context);
            return;
        }

        try
        {
            try
            {
                try
                {
                    await MultiLevelHandlerAsync(context, id, depth);
                }
                catch (InvalidOperationException ioEx)
                {
                    try
                    {
                        // Try-in-handler nesting #1
                        await LogErrorAsync($"[{id}] InvalidOp depth {depth}: {ioEx.Message}");
                        await DeepNestedChainAsync(context, id, depth + 1);
                    }
                    catch (Exception inner1)
                    {
                        try
                        {
                            // Try-in-handler nesting #2 (nested deeper)
                            await EmergencyLogAsync($"[{id}] Inner1 depth {depth}: {inner1.Message}");
                        }
                        catch (Exception inner2)
                        {
                            try
                            {
                                // Try-in-handler nesting #3 (even deeper)
                                Console.WriteLine($"[{id}] Inner2 depth {depth}: {inner2.Message}");
                                await RecoveryActionAsync(id, depth);
                            }
                            finally
                            {
                                try
                                {
                                    // Try-in-finally creates additional complexity
                                    await CleanupActionAsync(id, depth, "inner2");
                                }
                                catch (Exception cleanupEx)
                                {
                                    Console.WriteLine($"[{id}] Cleanup failed: {cleanupEx.Message}");
                                }
                            }
                        }
                        finally
                        {
                            try
                            {
                                await CleanupActionAsync(id, depth, "inner1");
                            }
                            catch (Exception finalEx)
                            {
                                try
                                {
                                    await LastResortAsync(id, depth);
                                }
                                catch (Exception lastEx)
                                {
                                    Console.WriteLine($"[{id}] Last resort failed: {lastEx.Message}");
                                }
                            }
                        }
                    }
                    finally
                    {
                        try
                        {
                            await CleanupActionAsync(id, depth, "InvalidOp");
                        }
                        catch (Exception cleanupEx)
                        {
                            try
                            {
                                await EmergencyCleanupAsync(id, depth);
                            }
                            catch (Exception emergencyEx)
                            {
                                Console.WriteLine($"[{id}] Emergency cleanup failed: {emergencyEx.Message}");
                            }
                        }
                    }
                }
                catch (UnauthorizedAccessException uaeEx)
                {
                    try
                    {
                        // Different exception type, more try-in-handler nesting
                        await LogErrorAsync($"[{id}] Unauthorized depth {depth}: {uaeEx.Message}");
                        
                        try
                        {
                            await RecoveryActionAsync(id, depth);
                            await DeepNestedChainAsync(context, id, depth + 1);
                        }
                        catch (TimeoutException timeoutEx)
                        {
                            try
                            {
                                await HandleTimeoutAsync(id, depth, timeoutEx);
                            }
                            catch (Exception timeoutInner)
                            {
                                try
                                {
                                    Console.WriteLine($"[{id}] Timeout inner: {timeoutInner.Message}");
                                    await LastResortAsync(id, depth);
                                }
                                finally
                                {
                                    try
                                    {
                                        await CleanupActionAsync(id, depth, "timeout");
                                    }
                                    catch (Exception) { /* Swallow */ }
                                }
                            }
                        }
                    }
                    catch (Exception uaeInner)
                    {
                        try
                        {
                            await LogErrorAsync($"[{id}] UAE inner depth {depth}: {uaeInner.Message}");
                        }
                        finally
                        {
                            try
                            {
                                await CleanupActionAsync(id, depth, "UAE");
                            }
                            catch (Exception cleanupEx)
                            {
                                Console.WriteLine($"[{id}] UAE cleanup failed: {cleanupEx.Message}");
                            }
                        }
                    }
                }
                catch (ArgumentException argEx)
                {
                    try
                    {
                        // Yet another exception type with deep try-in-handler nesting
                        await LogErrorAsync($"[{id}] Argument depth {depth}: {argEx.Message}");
                        
                        try
                        {
                            await RecoveryActionAsync(id, depth);
                        }
                        catch (NullReferenceException nullEx)
                        {
                            try
                            {
                                Console.WriteLine($"[{id}] Null ref: {nullEx.Message}");
                                try
                                {
                                    await EmergencyLogAsync($"[{id}] Emergency null handling");
                                }
                                catch (Exception emergencyEx)
                                {
                                    try
                                    {
                                        Console.WriteLine($"[{id}] Emergency failed: {emergencyEx.Message}");
                                    }
                                    finally
                                    {
                                        try
                                        {
                                            await LastResortAsync(id, depth);
                                        }
                                        catch (Exception) { /* Ultimate swallow */ }
                                    }
                                }
                            }
                            catch (Exception nullInner)
                            {
                                Console.WriteLine($"[{id}] Null inner: {nullInner.Message}");
                            }
                        }
                    }
                    catch (Exception argInner)
                    {
                        try
                        {
                            await LogErrorAsync($"[{id}] Arg inner: {argInner.Message}");
                        }
                        finally
                        {
                            await CleanupActionAsync(id, depth, "argument");
                        }
                    }
                }
            }
            catch (Exception level2Ex)
            {
                try
                {
                    await LogErrorAsync($"[{id}] Level2 depth {depth}: {level2Ex.Message}");
                    await DeepNestedChainAsync(context, id, depth + 1);
                }
                catch (Exception level2Inner)
                {
                    try
                    {
                        Console.WriteLine($"[{id}] Level2 inner: {level2Inner.Message}");
                    }
                    finally
                    {
                        try
                        {
                            await CleanupActionAsync(id, depth, "level2");
                        }
                        catch (Exception) { /* Swallow */ }
                    }
                }
            }
            finally
            {
                try
                {
                    await CleanupActionAsync(id, depth, "level1");
                }
                catch (Exception level1FinallyEx)
                {
                    try
                    {
                        Console.WriteLine($"[{id}] Level1 finally failed: {level1FinallyEx.Message}");
                        await EmergencyCleanupAsync(id, depth);
                    }
                    catch (Exception emergencyEx)
                    {
                        try
                        {
                            Console.WriteLine($"[{id}] Emergency in finally: {emergencyEx.Message}");
                        }
                        finally
                        {
                            try
                            {
                                await LastResortAsync(id, depth);
                            }
                            catch (Exception) { /* Ultimate fallback */ }
                        }
                    }
                }
            }
        }
        catch (Exception outerEx)
        {
            try
            {
                await LogErrorAsync($"[{id}] Outer depth {depth}: {outerEx.Message}");
            }
            finally
            {
                try
                {
                    await CleanupActionAsync(id, depth, "outer");
                }
                catch (Exception) { /* Final swallow */ }
            }
        }
    }

    private async Task MultiLevelHandlerAsync(HttpContext context, int id, int depth)
    {
        try
        {
            try
            {
                // Create some async operations to ensure complex state machine generation
                await Task.Delay(1);
                
                if (depth % 3 == 0)
                    throw new InvalidOperationException($"Test InvalidOp at depth {depth}");
                else if (depth % 3 == 1)
                    throw new UnauthorizedAccessException($"Test Unauthorized at depth {depth}");
                else if (depth % 3 == 2)
                    throw new ArgumentException($"Test Argument at depth {depth}");
                    
                await Task.Delay(1);
            }
            catch (Exception ex) when (ex.Message.Contains("Test"))
            {
                try
                {
                    // Exception filter with try-in-handler
                    await LogErrorAsync($"[{id}] Filtered: {ex.Message}");
                    throw; // Re-throw to outer handlers
                }
                catch (Exception filterEx)
                {
                    try
                    {
                        Console.WriteLine($"[{id}] Filter exception: {filterEx.Message}");
                        throw; // Ensure it propagates
                    }
                    finally
                    {
                        try
                        {
                            await CleanupActionAsync(id, depth, "filter");
                        }
                        catch (Exception) { /* Cleanup swallow */ }
                    }
                }
            }
        }
        catch (Exception multilevelEx)
        {
            try
            {
                await LogErrorAsync($"[{id}] Multilevel: {multilevelEx.Message}");
                throw; // Propagate to create more EH complexity
            }
            catch (Exception propagateEx)
            {
                try
                {
                    Console.WriteLine($"[{id}] Propagate: {propagateEx.Message}");
                    throw; // Keep propagating
                }
                finally
                {
                    try
                    {
                        await CleanupActionAsync(id, depth, "propagate");
                    }
                    catch (Exception) { /* Cleanup failure */ }
                }
            }
        }
    }

    private async Task LogErrorAsync(string message)
    {
        await Task.Delay(1); // Async operation
        Console.WriteLine($"LOG: {message}");
    }

    private async Task EmergencyLogAsync(string message)
    {
        await Task.Delay(1);
        Console.WriteLine($"EMERGENCY: {message}");
    }

    private async Task RecoveryActionAsync(int id, int depth)
    {
        await Task.Delay(1);
        Console.WriteLine($"[{id}] Recovery at depth {depth}");
    }

    private async Task CleanupActionAsync(int id, int depth, string context)
    {
        await Task.Delay(1);
        Console.WriteLine($"[{id}] Cleanup {context} at depth {depth}");
    }

    private async Task EmergencyCleanupAsync(int id, int depth)
    {
        await Task.Delay(1);
        Console.WriteLine($"[{id}] Emergency cleanup at depth {depth}");
    }

    private async Task LastResortAsync(int id, int depth)
    {
        await Task.Delay(1);
        Console.WriteLine($"[{id}] Last resort at depth {depth}");
    }

    private async Task HandleTimeoutAsync(int id, int depth, Exception ex)
    {
        await Task.Delay(1);
        Console.WriteLine($"[{id}] Timeout handling at depth {depth}: {ex.Message}");
    }
}