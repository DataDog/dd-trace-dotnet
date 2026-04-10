using System;
using System.Threading.Tasks;

namespace Samples.TraceAnnotations
{
    /// <summary>
    /// Test class with deeply nested try/catch/finally patterns that triggered
    /// a bug in the IL rewriter's exception handler sorting logic (APMS-19196).
    /// The bug causes InvalidProgramException on Linux x86/x64 due to stricter EH clause
    /// validation by the Linux CLR; Windows does not exhibit the crash.
    /// </summary>
    public static class ExtremeExceptionHandling
    {
        private static int _counter = 0;

        /// <summary>
        /// Async wrapper that calls the sync method with deep EH nesting.
        /// </summary>
        public static async Task<string> DeepNestedExceptionHandlingAsync()
        {
            await Task.Yield();
            return DeepNestedExceptionHandlingSync();
        }

        /// <summary>
        /// Synchronous method with 9 levels of nested try/catch/finally.
        /// This pattern triggers the EH clause sorting bug when instrumented.
        /// The EH is directly in this method (not in a state machine).
        /// </summary>
        public static string DeepNestedExceptionHandlingSync()
        {
            var requestId = System.Threading.Interlocked.Increment(ref _counter);

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
                                        Console.WriteLine($"[L9] {requestId}");
                                        System.Threading.Thread.Sleep(1);
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
    }
}
