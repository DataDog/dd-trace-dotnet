var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// APMS-19196: Simplified endpoint version - no middleware needed!
app.MapGet("/", CrashTriggerEndpoint);

app.Run();

// APMS-19196: The crashing nested EH pattern as a simple endpoint
static async Task<string> CrashTriggerEndpoint(HttpContext context)
{
    var requestId = Random.Shared.Next(1000, 9999);
    
    try
    {
        // Level 1
        try
        {
            Console.WriteLine($"[L1] {requestId}");
            
            // Level 5 (skipping L2,L3,L4 - the crashing pattern!)
            try
            {
                Console.WriteLine($"[L5] {requestId}");
                
                // Level 6
                try
                {
                    Console.WriteLine($"[L6] {requestId}");
                    
                    // Level 7
                    try
                    {
                        Console.WriteLine($"[L7] {requestId}");
                        
                        // Level 8
                        try
                        {
                            Console.WriteLine($"[L8] {requestId}");
                            
                            // Level 9 - THE CRASH TRIGGER
                            try
                            {
                                Console.WriteLine($"[L9] **CRASH TRIGGER** {requestId}");
                                await Task.Delay(1); // Simulate async work
                                Console.WriteLine($"[L9] Success {requestId}");
                            }
                            catch (Exception l9Ex)
                            {
                                Console.WriteLine($"[L9] Exception: {l9Ex.GetType().Name}");
                            }
                            finally
                            {
                                Console.WriteLine($"[L9] Finally {requestId}");
                            }
                        }
                        catch (Exception l8Ex)
                        {
                            Console.WriteLine($"[L8] Exception: {l8Ex.GetType().Name}");
                        }
                        finally
                        {
                            Console.WriteLine($"[L8] Finally {requestId}");
                        }
                    }
                    catch (Exception l7Ex)
                    {
                        Console.WriteLine($"[L7] Exception: {l7Ex.GetType().Name}");
                    }
                    finally
                    {
                        Console.WriteLine($"[L7] Finally {requestId}");
                    }
                }
                catch (Exception l6Ex)
                {
                    Console.WriteLine($"[L6] Exception: {l6Ex.GetType().Name}");
                }
                finally
                {
                    Console.WriteLine($"[L6] Finally {requestId}");
                }
            }
            catch (Exception l5Ex)
            {
                Console.WriteLine($"[L5] Exception: {l5Ex.GetType().Name}");
            }
            finally
            {
                Console.WriteLine($"[L5] Finally {requestId}");
            }
        }
        finally
        {
            Console.WriteLine($"[L1] Finally {requestId}");
        }
    }
    catch (Exception rootEx)
    {
        Console.WriteLine($"[ROOT] Exception: {rootEx.GetType().Name}");
    }
    finally
    {
        Console.WriteLine($"[ROOT] Finally {requestId}");
    }
    
    return $"Success {requestId}";
}
