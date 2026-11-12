extern alias DatadogTrace;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CallTargetNativeTest.NoOp;
using DatadogTrace::Datadog.Trace.ClrProfiler;

namespace CallTargetNativeTest;

/*
 * Real async methods covering the shapes the CallTarget async paths branch on. The default build compiles them to state
 * machines, and the DD_RuntimeAsync build (.NET 11+) compiles them as runtime-async, which the rewriter and
 * CallTargetInvoker.EndMethodRuntimeAsync handle very differently. Integrations must not be able to tell the difference,
 * so CallTargetNativeTests expects the same output from both builds, except where noted.
 *
 * "Completes" methods finish without suspending, and "Suspends" methods yield first, so a state-machine method is still
 * pending when its EndMethod runs.
 */
partial class Program
{
    private static void AddRuntimeAsyncDefinitions(List<NativeCallTargetDefinition> definitions)
    {
        // Noop0ArgumentsIntegration declares both OnMethodEnd and OnAsyncMethodEnd, and throws from whichever the
        // target's type name asks for
        AddDefinitionsForAllMethods(definitions, typeof(RuntimeAsyncShapes), typeof(Noop0ArgumentsIntegration));
        AddDefinitionsForAllMethods(definitions, typeof(RuntimeAsyncShapesGeneric<>), typeof(Noop0ArgumentsIntegration));
        AddDefinitionsForAllMethods(definitions, typeof(RuntimeAsyncShapesStruct), typeof(Noop0ArgumentsIntegration));
        AddDefinitionsForAllMethods(definitions, typeof(RuntimeAsyncShapesStatic), typeof(Noop0ArgumentsIntegration));
        AddDefinitionsForAllMethods(definitions, typeof(RuntimeAsyncExceptions), typeof(RuntimeAsyncFailureIntegration));
        AddDefinitionsForAllMethods(definitions, typeof(RuntimeAsyncThrowOnBegin), typeof(Noop0ArgumentsIntegration));
        AddDefinitionsForAllMethods(definitions, typeof(RuntimeAsyncThrowOnEnd), typeof(Noop0ArgumentsIntegration));
        AddDefinitionsForAllMethods(definitions, typeof(RuntimeAsyncThrowOnAsyncEnd), typeof(Noop0ArgumentsIntegration));

        // Integrations declaring only one of the end callbacks, or replacing the result
        AddDefinitionsForAllMethods(definitions, typeof(RuntimeAsyncAsyncEndOnly), typeof(RuntimeAsyncAsyncEndOnlyIntegration));
        AddDefinitionsForAllMethods(definitions, typeof(RuntimeAsyncMethodEndOnly), typeof(RuntimeAsyncMethodEndOnlyIntegration));
        AddDefinitionsForAllMethods(definitions, typeof(RuntimeAsyncReplaceResult), typeof(RuntimeAsyncReplaceResultIntegration));
        AddDefinitionsForAllMethods(definitions, typeof(RuntimeAsyncReplaceTask), typeof(RuntimeAsyncReplaceTaskIntegration));

        // Arguments: the fast path (with skipping the method body), and the object[] slow path from 9 arguments
        AddDefinitionsForAllMethods(definitions, typeof(RuntimeAsyncWith1Argument), typeof(Noop1ArgumentsIntegration));
        AddDefinitionsForAllMethods(definitions, typeof(RuntimeAsyncWith9Arguments), typeof(Noop9ArgumentsIntegration));

        AddDefinitionsForAllMethods(definitions, typeof(RuntimeAsyncThrowBubbleUpOnBegin), typeof(CallTargetBubbleUpExceptionsIntegrationAsync));
        AddDefinitionsForAllMethods(definitions, typeof(RuntimeAsyncThrowBubbleUpOnAsyncEnd), typeof(CallTargetBubbleUpExceptionsIntegrationAsync));
        AddDefinitionsForAllMethods(definitions, typeof(RuntimeAsyncThrowNestedBubbleUpOnAsyncEnd), typeof(CallTargetBubbleUpExceptionsIntegrationAsync));
    }

    private static void AddDefinitionsForAllMethods(List<NativeCallTargetDefinition> definitions, Type type, Type integration)
    {
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            var signature = Enumerable.Repeat("_", method.GetParameters().Length + 1).ToArray();
            definitions.Add(new(TargetAssembly, type.FullName, method.Name, signature, 0, 0, 0, 1, 1, 1, integrationAssembly, integration.FullName));
        }
    }

    private static void RuntimeAsync()
    {
        // Return shapes on each kind of declaring type
        var shapes = new RuntimeAsyncShapes();
        RunAsync(typeof(RuntimeAsyncShapes), nameof(RuntimeAsyncShapes.CompletesTask), () => shapes.CompletesTask());
        RunAsync(typeof(RuntimeAsyncShapes), nameof(RuntimeAsyncShapes.SuspendsTask), () => shapes.SuspendsTask());
        RunAsync(typeof(RuntimeAsyncShapes), nameof(RuntimeAsyncShapes.CompletesTaskOfInt), () => shapes.CompletesTaskOfInt(), 42);
        RunAsync(typeof(RuntimeAsyncShapes), nameof(RuntimeAsyncShapes.SuspendsTaskOfInt), () => shapes.SuspendsTaskOfInt(), 42);
        RunAsync(typeof(RuntimeAsyncShapes), nameof(RuntimeAsyncShapes.SuspendsTaskOfString), () => shapes.SuspendsTaskOfString(), "Hello World");
        RunAsync(typeof(RuntimeAsyncShapes), nameof(RuntimeAsyncShapes.CompletesValueTask), () => shapes.CompletesValueTask().AsTask());
        RunAsync(typeof(RuntimeAsyncShapes), nameof(RuntimeAsyncShapes.SuspendsValueTask), () => shapes.SuspendsValueTask().AsTask());
        RunAsync(typeof(RuntimeAsyncShapes), nameof(RuntimeAsyncShapes.CompletesValueTaskOfInt), () => shapes.CompletesValueTaskOfInt().AsTask(), 42);
        RunAsync(typeof(RuntimeAsyncShapes), nameof(RuntimeAsyncShapes.SuspendsValueTaskOfInt), () => shapes.SuspendsValueTaskOfInt().AsTask(), 42);
        RunAsync(typeof(RuntimeAsyncShapes), nameof(RuntimeAsyncShapes.SuspendsGenericMethod) + "<int>", () => shapes.SuspendsGenericMethod<int>(), 0);
        RunAsync(typeof(RuntimeAsyncShapes), nameof(RuntimeAsyncShapes.SuspendsGenericMethod) + "<string>", () => shapes.SuspendsGenericMethod<string>(), null);

        RunGenericShapes<int>(0);
        RunGenericShapes<string>(null);

        var shapesStruct = new RuntimeAsyncShapesStruct();
        RunAsync(typeof(RuntimeAsyncShapesStruct), nameof(RuntimeAsyncShapesStruct.CompletesTask), () => shapesStruct.CompletesTask());
        RunAsync(typeof(RuntimeAsyncShapesStruct), nameof(RuntimeAsyncShapesStruct.SuspendsTask), () => shapesStruct.SuspendsTask());
        RunAsync(typeof(RuntimeAsyncShapesStruct), nameof(RuntimeAsyncShapesStruct.SuspendsTaskOfInt), () => shapesStruct.SuspendsTaskOfInt(), 42);
        RunAsync(typeof(RuntimeAsyncShapesStruct), nameof(RuntimeAsyncShapesStruct.SuspendsValueTask), () => shapesStruct.SuspendsValueTask().AsTask());
        RunAsync(typeof(RuntimeAsyncShapesStruct), nameof(RuntimeAsyncShapesStruct.SuspendsValueTaskOfInt), () => shapesStruct.SuspendsValueTaskOfInt().AsTask(), 42);

        RunAsync(typeof(RuntimeAsyncShapesStatic), nameof(RuntimeAsyncShapesStatic.CompletesTask), () => RuntimeAsyncShapesStatic.CompletesTask());
        RunAsync(typeof(RuntimeAsyncShapesStatic), nameof(RuntimeAsyncShapesStatic.SuspendsTask), () => RuntimeAsyncShapesStatic.SuspendsTask());
        RunAsync(typeof(RuntimeAsyncShapesStatic), nameof(RuntimeAsyncShapesStatic.SuspendsTaskOfInt), () => RuntimeAsyncShapesStatic.SuspendsTaskOfInt(), 42);
        RunAsync(typeof(RuntimeAsyncShapesStatic), nameof(RuntimeAsyncShapesStatic.SuspendsValueTask), () => RuntimeAsyncShapesStatic.SuspendsValueTask().AsTask());
        RunAsync(typeof(RuntimeAsyncShapesStatic), nameof(RuntimeAsyncShapesStatic.SuspendsValueTaskOfInt), () => RuntimeAsyncShapesStatic.SuspendsValueTaskOfInt().AsTask(), 42);
        RunAsync(typeof(RuntimeAsyncShapesStatic), nameof(RuntimeAsyncShapesStatic.SuspendsGenericMethod) + "<string>", () => RuntimeAsyncShapesStatic.SuspendsGenericMethod<string>(), null);

        RuntimeAsyncFailures();

        // Exceptions thrown by the integration, which must not reach the target's caller
        var throwOnBegin = new RuntimeAsyncThrowOnBegin();
        RunAsync(typeof(RuntimeAsyncThrowOnBegin), nameof(RuntimeAsyncThrowOnBegin.CompletesTaskOfInt), () => throwOnBegin.CompletesTaskOfInt(), 42);
        RunAsync(typeof(RuntimeAsyncThrowOnBegin), nameof(RuntimeAsyncThrowOnBegin.SuspendsTaskOfInt), () => throwOnBegin.SuspendsTaskOfInt(), 42);
        RunAsync(typeof(RuntimeAsyncThrowOnBegin), nameof(RuntimeAsyncThrowOnBegin.SuspendsTask), () => throwOnBegin.SuspendsTask());

        // Only targets that complete without suspending: when a state-machine target is still pending, a throwing
        // OnMethodEnd orphans the OnAsyncMethodEnd continuation, which may then not have run by the time we check
        var throwOnEnd = new RuntimeAsyncThrowOnEnd();
        RunAsync(typeof(RuntimeAsyncThrowOnEnd), nameof(RuntimeAsyncThrowOnEnd.CompletesTaskOfInt), () => throwOnEnd.CompletesTaskOfInt(), 42);
        RunAsync(typeof(RuntimeAsyncThrowOnEnd), nameof(RuntimeAsyncThrowOnEnd.CompletesTask), () => throwOnEnd.CompletesTask());

        // A throwing OnAsyncMethodEnd must not reach the caller, nor stop OnMethodEnd from running
        var throwOnAsyncEnd = new RuntimeAsyncThrowOnAsyncEnd();
        RunAsync(typeof(RuntimeAsyncThrowOnAsyncEnd), nameof(RuntimeAsyncThrowOnAsyncEnd.CompletesTaskOfInt), () => throwOnAsyncEnd.CompletesTaskOfInt(), 42);
        RunAsync(typeof(RuntimeAsyncThrowOnAsyncEnd), nameof(RuntimeAsyncThrowOnAsyncEnd.CompletesTask), () => throwOnAsyncEnd.CompletesTask());
        RunAsync(typeof(RuntimeAsyncThrowOnAsyncEnd), nameof(RuntimeAsyncThrowOnAsyncEnd.SuspendsTaskOfInt), () => throwOnAsyncEnd.SuspendsTaskOfInt(), 42);
        RunAsync(typeof(RuntimeAsyncThrowOnAsyncEnd), nameof(RuntimeAsyncThrowOnAsyncEnd.SuspendsTask), () => throwOnAsyncEnd.SuspendsTask());

        // Integrations declaring only one end callback
        var asyncEndOnly = new RuntimeAsyncAsyncEndOnly();
        RunAsync(typeof(RuntimeAsyncAsyncEndOnly), nameof(RuntimeAsyncAsyncEndOnly.SuspendsTask), () => asyncEndOnly.SuspendsTask());
        RunAsync(typeof(RuntimeAsyncAsyncEndOnly), nameof(RuntimeAsyncAsyncEndOnly.SuspendsTaskOfInt), () => asyncEndOnly.SuspendsTaskOfInt(), 42);
        var methodEndOnly = new RuntimeAsyncMethodEndOnly();
        RunAsync(typeof(RuntimeAsyncMethodEndOnly), nameof(RuntimeAsyncMethodEndOnly.SuspendsTask), () => methodEndOnly.SuspendsTask(), asyncMethod: false);
        RunAsync(typeof(RuntimeAsyncMethodEndOnly), nameof(RuntimeAsyncMethodEndOnly.SuspendsTaskOfInt), () => methodEndOnly.SuspendsTaskOfInt(), 42, asyncMethod: false);

        // Integrations replacing the result, which the caller must observe
        var replaceResult = new RuntimeAsyncReplaceResult();
        RunAsync(typeof(RuntimeAsyncReplaceResult), nameof(RuntimeAsyncReplaceResult.CompletesTaskOfInt), () => replaceResult.CompletesTaskOfInt(), RuntimeAsyncReplaceResultIntegration.IntReplacement);
        RunAsync(typeof(RuntimeAsyncReplaceResult), nameof(RuntimeAsyncReplaceResult.SuspendsTaskOfInt), () => replaceResult.SuspendsTaskOfInt(), RuntimeAsyncReplaceResultIntegration.IntReplacement);
        RunAsync(typeof(RuntimeAsyncReplaceResult), nameof(RuntimeAsyncReplaceResult.SuspendsTaskOfString), () => replaceResult.SuspendsTaskOfString(), RuntimeAsyncReplaceResultIntegration.StringReplacement);
        RunAsync(typeof(RuntimeAsyncReplaceResult), nameof(RuntimeAsyncReplaceResult.SuspendsValueTaskOfInt), () => replaceResult.SuspendsValueTaskOfInt().AsTask(), RuntimeAsyncReplaceResultIntegration.IntReplacement);
        var replaceTask = new RuntimeAsyncReplaceTask();
        RunAsync(typeof(RuntimeAsyncReplaceTask), nameof(RuntimeAsyncReplaceTask.SuspendsTaskOfInt), () => replaceTask.SuspendsTaskOfInt(), RuntimeAsyncReplaceResultIntegration.IntReplacement, asyncMethod: false);
        RunAsync(typeof(RuntimeAsyncReplaceTask), nameof(RuntimeAsyncReplaceTask.SuspendsValueTaskOfInt), () => replaceTask.SuspendsValueTaskOfInt().AsTask(), RuntimeAsyncReplaceResultIntegration.IntReplacement, asyncMethod: false);

        // Arguments
        var with1Argument = new RuntimeAsyncWith1Argument();
        RunAsync(typeof(RuntimeAsyncWith1Argument), nameof(RuntimeAsyncWith1Argument.SuspendsTask), () => with1Argument.SuspendsTask("Hello world"));
        RunAsync(typeof(RuntimeAsyncWith1Argument), nameof(RuntimeAsyncWith1Argument.SuspendsTaskOfInt), () => with1Argument.SuspendsTaskOfInt("Hello world"), 42);
#if RUNTIME_ASYNC_BUILD
        // A skipped state-machine method returns a null task, so this only works for a runtime-async one, whose skipped
        // body leaves default(T) for the runtime to build a completed task from
        RunAsync(typeof(RuntimeAsyncWith1Argument), nameof(RuntimeAsyncWith1Argument.SuspendsTask), () => with1Argument.SuspendsTask(Noop1ArgumentsIntegration.SKIPMETHODBODY));
        RunAsync(typeof(RuntimeAsyncWith1Argument), nameof(RuntimeAsyncWith1Argument.SuspendsTaskOfInt), () => with1Argument.SuspendsTaskOfInt(Noop1ArgumentsIntegration.SKIPMETHODBODY), 0);
#endif

        var with9Arguments = new RuntimeAsyncWith9Arguments();
        RunAsync(typeof(RuntimeAsyncWith9Arguments), nameof(RuntimeAsyncWith9Arguments.SuspendsTask), () => with9Arguments.SuspendsTask("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null));
        RunAsync(typeof(RuntimeAsyncWith9Arguments), nameof(RuntimeAsyncWith9Arguments.SuspendsTaskOfInt), () => with9Arguments.SuspendsTaskOfInt("Hello world", 42, Tuple.Create(1, 2), Task.CompletedTask, CancellationToken.None, 987, "Arg7-Value", Assembly.GetExecutingAssembly(), null), 42);

        // CallTargetBubbleUpException must reach the caller, including from an OnAsyncMethodEnd that a state-machine
        // target which suspended runs in a continuation
        var bubbleUpOnBegin = new RuntimeAsyncThrowBubbleUpOnBegin();
        Console.WriteLine($"{typeof(RuntimeAsyncThrowBubbleUpOnBegin).FullName}.{nameof(RuntimeAsyncThrowBubbleUpOnBegin.CompletesTask)}");
        RunMethod(() => bubbleUpOnBegin.CompletesTask().GetAwaiter().GetResult(), bubblingUpException: true, asyncMethod: true);
        var bubbleUpOnAsyncEnd = new RuntimeAsyncThrowBubbleUpOnAsyncEnd();
        Console.WriteLine($"{typeof(RuntimeAsyncThrowBubbleUpOnAsyncEnd).FullName}.{nameof(RuntimeAsyncThrowBubbleUpOnAsyncEnd.CompletesTask)}");
        RunMethod(() => bubbleUpOnAsyncEnd.CompletesTask().GetAwaiter().GetResult(), bubblingUpException: true, asyncMethod: true);
        Console.WriteLine($"{typeof(RuntimeAsyncThrowBubbleUpOnAsyncEnd).FullName}.{nameof(RuntimeAsyncThrowBubbleUpOnAsyncEnd.SuspendsTask)}");
        RunMethod(() => bubbleUpOnAsyncEnd.SuspendsTask().GetAwaiter().GetResult(), bubblingUpException: true, asyncMethod: true);
        var nestedBubbleUpOnAsyncEnd = new RuntimeAsyncThrowNestedBubbleUpOnAsyncEnd();
        Console.WriteLine($"{typeof(RuntimeAsyncThrowNestedBubbleUpOnAsyncEnd).FullName}.{nameof(RuntimeAsyncThrowNestedBubbleUpOnAsyncEnd.CompletesTaskOfInt)}");
        RunMethod(() => nestedBubbleUpOnAsyncEnd.CompletesTaskOfInt().GetAwaiter().GetResult(), bubblingUpException: true, asyncMethod: true);
        Console.WriteLine($"{typeof(RuntimeAsyncThrowNestedBubbleUpOnAsyncEnd).FullName}.{nameof(RuntimeAsyncThrowNestedBubbleUpOnAsyncEnd.SuspendsTaskOfInt)}");
        RunMethod(() => nestedBubbleUpOnAsyncEnd.SuspendsTaskOfInt().GetAwaiter().GetResult(), bubblingUpException: true, asyncMethod: true);
    }

    private static void RuntimeAsyncFailures()
    {
        // Exceptions thrown by the target method, before and after it first suspends.
        var exceptions = new RuntimeAsyncExceptions();
        RunAsyncThrows(typeof(RuntimeAsyncExceptions), nameof(RuntimeAsyncExceptions.ThrowsBeforeSuspendingTaskOfInt), () => exceptions.ThrowsBeforeSuspendingTaskOfInt());
        RunAsyncThrows(typeof(RuntimeAsyncExceptions), nameof(RuntimeAsyncExceptions.ThrowsAfterSuspendingTaskOfInt), () => exceptions.ThrowsAfterSuspendingTaskOfInt());
        RunAsyncThrows(typeof(RuntimeAsyncExceptions), nameof(RuntimeAsyncExceptions.ThrowsAfterSuspendingTask), () => exceptions.ThrowsAfterSuspendingTask());
        RunAsyncThrows(typeof(RuntimeAsyncExceptions), nameof(RuntimeAsyncExceptions.ThrowsAfterSuspendingValueTask), () => exceptions.ThrowsAfterSuspendingValueTask().AsTask());
        RunAsyncThrows(typeof(RuntimeAsyncExceptions), nameof(RuntimeAsyncExceptions.ThrowsAfterSuspendingValueTaskOfInt), () => exceptions.ThrowsAfterSuspendingValueTaskOfInt().AsTask());

        RunAsyncThrows(typeof(RuntimeAsyncExceptions), nameof(RuntimeAsyncExceptions.CancelsAfterSuspendingTask), () => exceptions.CancelsAfterSuspendingTask(), canceled: true);
        RunAsyncThrows(typeof(RuntimeAsyncExceptions), nameof(RuntimeAsyncExceptions.CancelsAfterSuspendingTaskOfInt), () => exceptions.CancelsAfterSuspendingTaskOfInt(), canceled: true);
        RunAsyncThrows(typeof(RuntimeAsyncExceptions), nameof(RuntimeAsyncExceptions.CancelsAfterSuspendingValueTask), () => exceptions.CancelsAfterSuspendingValueTask().AsTask(), canceled: true);
        RunAsyncThrows(typeof(RuntimeAsyncExceptions), nameof(RuntimeAsyncExceptions.CancelsAfterSuspendingValueTaskOfInt), () => exceptions.CancelsAfterSuspendingValueTaskOfInt().AsTask(), canceled: true);
    }

    private static void RunGenericShapes<T>(T expected)
    {
        var shapes = new RuntimeAsyncShapesGeneric<T>();
        var type = typeof(RuntimeAsyncShapesGeneric<T>);
        RunAsync(type, nameof(RuntimeAsyncShapesGeneric<T>.CompletesTask), () => shapes.CompletesTask());
        RunAsync(type, nameof(RuntimeAsyncShapesGeneric<T>.SuspendsTask), () => shapes.SuspendsTask());
        RunAsync(type, nameof(RuntimeAsyncShapesGeneric<T>.CompletesTaskOfT), () => shapes.CompletesTaskOfT(), expected);
        RunAsync(type, nameof(RuntimeAsyncShapesGeneric<T>.SuspendsTaskOfT), () => shapes.SuspendsTaskOfT(), expected);
        RunAsync(type, nameof(RuntimeAsyncShapesGeneric<T>.SuspendsValueTaskOfT), () => shapes.SuspendsValueTaskOfT().AsTask(), expected);
    }

    private static void RunAsync(Type type, string methodName, Func<Task> action, bool asyncMethod = true)
    {
        Console.WriteLine($"{type.FullName}.{methodName}");
        RunMethod(() => action().GetAwaiter().GetResult(), asyncMethod: asyncMethod);
    }

    private static void RunAsync<T>(Type type, string methodName, Func<Task<T>> action, T expected, bool asyncMethod = true)
    {
        Console.WriteLine($"{type.FullName}.{methodName}");
        RunMethod(
            () =>
            {
                var result = action().GetAwaiter().GetResult();
                if (!EqualityComparer<T>.Default.Equals(result, expected))
                {
                    throw new Exception($"Expected {methodName} to return '{expected}' but it returned '{result}'");
                }
            },
            asyncMethod: asyncMethod);
    }

    private static void RunAsyncThrows(Type type, string methodName, Func<Task> action, bool canceled = false)
    {
        Console.WriteLine($"{type.FullName}.{methodName}");
        RuntimeAsyncFailureIntegration.Reset();
        RunMethod(
            () =>
            {
                try
                {
                    action().GetAwaiter().GetResult();
                }
                catch (Exception ex) when (canceled ? ex is OperationCanceledException : ex is InvalidOperationException && ex.Message == RuntimeAsyncExceptions.Message)
                {
                    ValidateAsyncFailure(ex);
                    return;
                }

                throw new Exception($"Expected {methodName} to throw");
            },
            asyncMethod: true);
    }

    private static void ValidateAsyncFailure(Exception exception)
    {
        if (RuntimeAsyncFailureIntegration.MethodEndException is not null)
        {
            throw new Exception($"OnMethodEnd received {RuntimeAsyncFailureIntegration.MethodEndException.GetType().Name} directly; return value: {RuntimeAsyncFailureIntegration.MethodEndReturnValue ?? "(null)"}");
        }

        if (!ReferenceEquals(RuntimeAsyncFailureIntegration.AsyncMethodEndException, exception))
        {
            throw new Exception("OnAsyncMethodEnd did not receive the original exception");
        }

        // After awaiting the call, the task captured by OnMethodEnd must carry the failure too.
        // Inspect ValueTask status without consuming its result a second time.
        var canceled = exception is OperationCanceledException;
        var carriesFailure = RuntimeAsyncFailureIntegration.MethodEndReturnValue switch
        {
            Task task => canceled ? task.IsCanceled : task.IsFaulted,
            ValueTask task => canceled ? task.IsCanceled : task.IsFaulted,
            ValueTask<int> task => canceled ? task.IsCanceled : task.IsFaulted,
            _ => false,
        };

        if (!carriesFailure)
        {
            throw new Exception("OnMethodEnd did not receive a task carrying the async failure");
        }
    }
}

internal class RuntimeAsyncShapes
{
    public async Task CompletesTask() => await Task.CompletedTask;

    public async Task SuspendsTask() => await Task.Yield();

    public async Task<int> CompletesTaskOfInt()
    {
        await Task.CompletedTask;
        return 42;
    }

    public async Task<int> SuspendsTaskOfInt()
    {
        await Task.Yield();
        return 42;
    }

    public async Task<string> SuspendsTaskOfString()
    {
        await Task.Yield();
        return "Hello World";
    }

    public async ValueTask CompletesValueTask() => await Task.CompletedTask;

    public async ValueTask SuspendsValueTask() => await Task.Yield();

    public async ValueTask<int> CompletesValueTaskOfInt()
    {
        await Task.CompletedTask;
        return 42;
    }

    public async ValueTask<int> SuspendsValueTaskOfInt()
    {
        await Task.Yield();
        return 42;
    }

    public async Task<TM> SuspendsGenericMethod<TM>()
    {
        await Task.Yield();
        return default;
    }
}

internal class RuntimeAsyncShapesGeneric<T>
{
    public async Task CompletesTask() => await Task.CompletedTask;

    public async Task SuspendsTask() => await Task.Yield();

    public async Task<T> CompletesTaskOfT()
    {
        await Task.CompletedTask;
        return default;
    }

    public async Task<T> SuspendsTaskOfT()
    {
        await Task.Yield();
        return default;
    }

    public async ValueTask<T> SuspendsValueTaskOfT()
    {
        await Task.Yield();
        return default;
    }
}

internal struct RuntimeAsyncShapesStruct
{
    public async Task CompletesTask() => await Task.CompletedTask;

    public async Task SuspendsTask() => await Task.Yield();

    public async Task<int> SuspendsTaskOfInt()
    {
        await Task.Yield();
        return 42;
    }

    public async ValueTask SuspendsValueTask() => await Task.Yield();

    public async ValueTask<int> SuspendsValueTaskOfInt()
    {
        await Task.Yield();
        return 42;
    }
}

internal static class RuntimeAsyncShapesStatic
{
    public static async Task CompletesTask() => await Task.CompletedTask;

    public static async Task SuspendsTask() => await Task.Yield();

    public static async Task<int> SuspendsTaskOfInt()
    {
        await Task.Yield();
        return 42;
    }

    public static async ValueTask SuspendsValueTask() => await Task.Yield();

    public static async ValueTask<int> SuspendsValueTaskOfInt()
    {
        await Task.Yield();
        return 42;
    }

    public static async Task<TM> SuspendsGenericMethod<TM>()
    {
        await Task.Yield();
        return default;
    }
}

internal class RuntimeAsyncExceptions
{
    public const string Message = "Expected exception from the target method";

    public async Task<int> ThrowsBeforeSuspendingTaskOfInt()
    {
        Throw();
        await Task.Yield();
        return 42;
    }

    public async Task<int> ThrowsAfterSuspendingTaskOfInt()
    {
        await Task.Yield();
        Throw();
        return 42;
    }

    public async Task ThrowsAfterSuspendingTask()
    {
        await Task.Yield();
        Throw();
    }

    public async ValueTask<int> ThrowsAfterSuspendingValueTaskOfInt()
    {
        await Task.Yield();
        Throw();
        return 42;
    }

    public async ValueTask ThrowsAfterSuspendingValueTask()
    {
        await Task.Yield();
        Throw();
    }

    public async Task CancelsAfterSuspendingTask()
    {
        await Task.Yield();
        throw new OperationCanceledException();
    }

    public async Task<int> CancelsAfterSuspendingTaskOfInt()
    {
        await Task.Yield();
        throw new OperationCanceledException();
    }

    public async ValueTask CancelsAfterSuspendingValueTask()
    {
        await Task.Yield();
        throw new OperationCanceledException();
    }

    public async ValueTask<int> CancelsAfterSuspendingValueTaskOfInt()
    {
        await Task.Yield();
        throw new OperationCanceledException();
    }

    private static void Throw() => throw new InvalidOperationException(Message);
}

internal class RuntimeAsyncThrowOnBegin
{
    public async Task<int> CompletesTaskOfInt()
    {
        await Task.CompletedTask;
        return 42;
    }

    public async Task<int> SuspendsTaskOfInt()
    {
        await Task.Yield();
        return 42;
    }

    public async Task SuspendsTask() => await Task.Yield();
}

internal class RuntimeAsyncThrowOnEnd
{
    public async Task<int> CompletesTaskOfInt()
    {
        await Task.CompletedTask;
        return 42;
    }

    public async Task CompletesTask() => await Task.CompletedTask;
}

internal class RuntimeAsyncThrowOnAsyncEnd
{
    public async Task<int> CompletesTaskOfInt()
    {
        await Task.CompletedTask;
        return 42;
    }

    public async Task CompletesTask() => await Task.CompletedTask;

    public async Task<int> SuspendsTaskOfInt()
    {
        await Task.Yield();
        return 42;
    }

    public async Task SuspendsTask() => await Task.Yield();
}

internal class RuntimeAsyncAsyncEndOnly
{
    public async Task SuspendsTask() => await Task.Yield();

    public async Task<int> SuspendsTaskOfInt()
    {
        await Task.Yield();
        return 42;
    }
}

internal class RuntimeAsyncMethodEndOnly
{
    public async Task SuspendsTask() => await Task.Yield();

    public async Task<int> SuspendsTaskOfInt()
    {
        await Task.Yield();
        return 42;
    }
}

internal class RuntimeAsyncReplaceResult
{
    public async Task<int> CompletesTaskOfInt()
    {
        await Task.CompletedTask;
        return 42;
    }

    public async Task<int> SuspendsTaskOfInt()
    {
        await Task.Yield();
        return 42;
    }

    public async Task<string> SuspendsTaskOfString()
    {
        await Task.Yield();
        return "Hello World";
    }

    public async ValueTask<int> SuspendsValueTaskOfInt()
    {
        await Task.Yield();
        return 42;
    }
}

internal class RuntimeAsyncReplaceTask
{
    public async Task<int> SuspendsTaskOfInt()
    {
        await Task.Yield();
        return 42;
    }

    public async ValueTask<int> SuspendsValueTaskOfInt()
    {
        await Task.Yield();
        return 42;
    }
}

internal class RuntimeAsyncWith1Argument
{
    public async Task SuspendsTask(string arg1) => await Task.Yield();

    public async Task<int> SuspendsTaskOfInt(string arg1)
    {
        await Task.Yield();
        return 42;
    }
}

internal class RuntimeAsyncWith9Arguments
{
    public async Task SuspendsTask(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9) => await Task.Yield();

    public async Task<int> SuspendsTaskOfInt(string arg1, int arg2, object arg3, Task arg4, CancellationToken arg5, ulong arg6, string arg7, Assembly arg8, int? arg9)
    {
        await Task.Yield();
        return 42;
    }
}

internal class RuntimeAsyncThrowBubbleUpOnBegin
{
    public async Task CompletesTask() => await Task.CompletedTask;
}

internal class RuntimeAsyncThrowBubbleUpOnAsyncEnd
{
    public async Task CompletesTask() => await Task.CompletedTask;

    public async Task SuspendsTask() => await Task.Yield();
}

internal class RuntimeAsyncThrowNestedBubbleUpOnAsyncEnd
{
    public async Task<int> CompletesTaskOfInt()
    {
        await Task.CompletedTask;
        return 42;
    }

    public async Task<int> SuspendsTaskOfInt()
    {
        await Task.Yield();
        return 42;
    }
}
