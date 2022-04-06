using System;

public class Logger<T>
{
    public void LogInformation(string message)
    {
        Console.WriteLine($"{typeof(T).Name}: {message}");
    }

    public void LogError(string message) => LogError(null, message);
    public void LogError(Exception ex, string message)
    {
        try
        {
            Console.SetOut(Console.Error);

            if (ex is not null)
            {
                Console.WriteLine($"{typeof(T).Name}: {ex}");
            }
            Console.WriteLine($"{typeof(T).Name}: {message}");
        }
        finally
        {
            Console.SetOut(Console.Out);
        }
    }
}
