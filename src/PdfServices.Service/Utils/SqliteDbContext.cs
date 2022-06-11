using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace PdfServices.Service.Utils;

public class SqliteDbContext : IDisposable
{
    private const string CONFIG_ROOT_NAMESPACE = "SqliteStorage";
    private const string CONFIG_USE_INMEMORY_DB = $"{CONFIG_ROOT_NAMESPACE}:UseMemoryDb";
    private const string CONFIG_SCRIPTS_PATH = $"{CONFIG_ROOT_NAMESPACE}:ScriptsPath";
    private const string CONFIG_DB_STORAGE_PATH = $"{CONFIG_ROOT_NAMESPACE}:DbStoragePath";
    
    private const string SECTION_NAME = "SqliteStorage";
    private const SqliteCacheMode CACHE_MODE = SqliteCacheMode.Shared;
    private readonly SqliteConnection _connection;
    
    public SqliteDbContext(IConfiguration config)
    {
        var use_inmemory = config.GetValue<bool?>(CONFIG_USE_INMEMORY_DB) ?? false;
        var scripts_path = config.GetValue<string>(CONFIG_SCRIPTS_PATH);
        if (string.IsNullOrEmpty(scripts_path) || !Directory.Exists(scripts_path)) {
            throw new Exception(GetErrReqConfigMsg(nameof(CONFIG_SCRIPTS_PATH), nameof(String)));
        }
        
        if (use_inmemory && !Directory.Exists(scripts_path)) {
            throw new Exception("A valid SQL scripts path is required");
        }

        var storage_path = config.GetValue<string>(CONFIG_DB_STORAGE_PATH);
        if (string.IsNullOrEmpty(storage_path)) {
            throw new Exception(GetErrReqConfigMsg(nameof(CONFIG_DB_STORAGE_PATH), nameof(String)));
        }

        if (!use_inmemory && !Directory.Exists(storage_path)) {
            Directory.CreateDirectory(storage_path);
        }
        
        var database_path = Path.Join(storage_path, "storage.db");
        var conn_builder = new SqliteConnectionStringBuilder {
            DataSource = use_inmemory ? null : database_path,
            Cache = CACHE_MODE,
            Mode = use_inmemory ? SqliteOpenMode.Memory : SqliteOpenMode.ReadWriteCreate
        };

        _connection = new SqliteConnection(conn_builder.ToString());
        _connection.Open();
        
        var sql_cmd = _connection.CreateCommand();
        foreach (var item in Directory.GetFiles(scripts_path, "*.sql")) {
            var sql_script = File.ReadAllText(item);
            sql_cmd.CommandText = sql_script;
            try {
                sql_cmd.ExecuteNonQuery();
            } catch {
                // ignored
            }
        }
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private static string GetErrReqConfigMsg(string paramName, string paramTypeName)
    {
        return $"required config param '{SECTION_NAME}:{paramName}' with type '{paramTypeName}' is not valid";
    }

    public SqliteConnection GetConnection()
    {
        return _connection;
    }
}