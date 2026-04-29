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
        /// Verifies that multiple catch handlers sharing the same try block dispatch
        /// to the correct handler after instrumentation. The CLR uses the first matching
        /// handler, so reordering would silently change exception dispatch semantics.
        /// </summary>
        public static int SameTryMultipleCatch(Exception ex)
        {
            try
            {
                throw ex;
            }
            catch (ArgumentNullException)
            {
                return 1;
            }
            catch (InvalidOperationException)
            {
                return 2;
            }
            catch (Exception)
            {
                return 3;
            }
        }

        /// <summary>
        /// Verifies that exception filter clauses mixed with typed catches preserve
        /// evaluation order after instrumentation. Filters are evaluated sequentially;
        /// reordering would change which filter matches first.
        /// </summary>
        public static int FilterAndTypedCatch(Exception ex)
        {
            try
            {
                throw ex;
            }
            catch (Exception e) when (e is ArgumentNullException)
            {
                return 1;
            }
            catch (Exception e) when (e is InvalidOperationException)
            {
                return 2;
            }
            catch (Exception)
            {
                return 3;
            }
        }

        /// <summary>
        /// Validates both multi-catch and filter ordering scenarios. Throws if any
        /// handler dispatches incorrectly, which would fail the integration test.
        /// </summary>
        public static void ValidateExceptionHandlerOrdering()
        {
            // Multi-catch ordering
            AssertEqual(1, SameTryMultipleCatch(new ArgumentNullException()), nameof(SameTryMultipleCatch), "ArgumentNullException");
            AssertEqual(2, SameTryMultipleCatch(new InvalidOperationException()), nameof(SameTryMultipleCatch), "InvalidOperationException");
            AssertEqual(3, SameTryMultipleCatch(new Exception()), nameof(SameTryMultipleCatch), "Exception");

            // Filter ordering
            AssertEqual(1, FilterAndTypedCatch(new ArgumentNullException()), nameof(FilterAndTypedCatch), "ArgumentNullException");
            AssertEqual(2, FilterAndTypedCatch(new InvalidOperationException()), nameof(FilterAndTypedCatch), "InvalidOperationException");
            AssertEqual(3, FilterAndTypedCatch(new Exception()), nameof(FilterAndTypedCatch), "Exception");

            Console.WriteLine("Exception handler ordering validated successfully");
        }

        private static void AssertEqual(int expected, int actual, string method, string exceptionType)
        {
            if (expected != actual)
            {
                throw new InvalidOperationException(
                    $"{method}({exceptionType}) returned {actual}, expected {expected}. " +
                    "Exception handler ordering was not preserved after instrumentation.");
            }
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
