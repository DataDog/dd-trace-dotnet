using System.ComponentModel;
using System.Data.SQLite;
using System.Diagnostics;
using MongoDB.Driver;
using Samples.Security.Data;

namespace Datadog.Trace.Annotations.Controllers;

public class QueryData
{
    public string Query { get; set; }
    public int IntField { get; set; }

    public List<string> Arguments { get; set; }

    public Dictionary<string, string> StringMap { get; set; }

    public string[] StringArrayArguments { get; set; }

    public QueryData InnerQuery { get; set; }
}

public class IastController
{
    private static SQLiteConnection? _dbConnection;
    private static IMongoDatabase? _mongoDb;

    public static IastController Instance { get; } = new();

    public SQLiteConnection? DbConnection
    {
        get { return _dbConnection ??= IastControllerHelper.CreateDatabase(); }
    }

    public IMongoDatabase MongoDb
    {
        get { return _mongoDb ??= MongoDbHelper.CreateMongoDb(); }
    }

    public static string? ExecuteCommandInternal(string? file, string? argumentLine = "")
    {
        try
        {
            if (!string.IsNullOrEmpty(file))
            {
                var result = Process.Start(file, argumentLine);
                return "Process launched: " + result.ProcessName;
            }

            return "No file provided";
        }
        catch (Win32Exception ex)
        {
            return IastControllerHelper.ToFormattedString(ex);
        }
        catch (Exception ex)
        {
            return IastControllerHelper.ToFormattedString(ex);
        }
    }

    public string Query(QueryData query)
    {
        if (!string.IsNullOrEmpty(query.Query))
        {
            return ExecuteQuery(query.Query);
        }

        if (query.Arguments is not null)
        {
            foreach (var value in query.Arguments)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    return ExecuteQuery(value);
                }
            }
        }

        if (query.StringArrayArguments is not null)
        {
            foreach (var value in query.StringArrayArguments)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    return ExecuteQuery(value);
                }
            }
        }

        if (query.StringMap is not null)
        {
            foreach (var value in query.StringMap)
            {
                if (!string.IsNullOrEmpty(value.Value))
                {
                    return ExecuteQuery(value.Value);
                }

                if (!string.IsNullOrEmpty(value.Key))
                {
                    return ExecuteQuery(value.Key);
                }
            }
        }

        if (query.InnerQuery != null)
        {
            return Query(query.InnerQuery);
        }

        return "No query or username was provided";
    }

    private string ExecuteQuery(string query)
    {
        var rname = new SQLiteCommand(query, DbConnection).ExecuteScalar();
        return "Result: " + rname;
    }
    
    public static CookieOptions GetDefaultCookieOptionsInstance()
    {
        var cookieOptions = new CookieOptions();
        cookieOptions.SameSite = SameSiteMode.Strict;
        cookieOptions.HttpOnly = true;
        cookieOptions.Secure = true;

        return cookieOptions;
    }
    
    public static string CopyStringAvoidTainting(string? original)
    {
        if (original is null) return string.Empty;
        return new string(original.AsEnumerable().ToArray());
    }
}
