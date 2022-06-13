using System;
using io = System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using PdfServices.Service.Utils;
using Dapper;

namespace PdfServices.Service.Services;

public class LocalStorageService : BackgroundService
{
    private const string SQL_DB_TEXT = @"
        create table FileCleanupQueue
        (
            Path         text     not null,
            TimeToExpire datetime not null
        );

        create unique index FileCleanupQueue_Path_uindex
            on FileCleanupQueue (Path);
    ";
    
    
    private const string UPSERT_CLEANUP_ITEM = "INSERT INTO FileCleanupQueue (Path, TimeToExpire) VALUES (@Path, @TimeToExpire) ON CONFLICT(Path) DO UPDATE SET TimeToExpire = @TimeToExpire";

    private const string SELECT_TIME_TO_EXPIRE = "SELECT TimeToExpire FROM FileCleanupQueue WHERE Path = @Path";
    private const string SELECT_EXPIRED_ITEMS =
        "SELECT Path FROM FileCleanupQueue WHERE TimeToExpire <= @CurrentDateTimeUtc";

    private const string DELETE_CLEANUP_ITEMS = "DELETE FROM FileCleanupQueue WHERE TimeToExpire <= @CurrentDateTimeUtc";
    
    private static readonly SqliteConnection MemDbConnection;

    private static string GetChunkBasePath() => io.Path.Combine(io.Path.GetTempPath(), "kwapi-pdf-services", "chunks");
    private static string GetOutputBasePath() => io.Path.Combine(io.Path.GetTempPath(), "kwapi-pdf-services", "output");

    internal enum StorageType
    {
        Chunk,
        Output
    }

    internal struct StorageHandle
    {
        public Guid Id { get; init; }
        public string FullPath { get; init; }
        public DateTime TimeToExpireUtc { get; init; }
    }

    static LocalStorageService()
    {
        var conn_builder = new SqliteConnectionStringBuilder {
            DataSource = null,
            Cache = SqliteCacheMode.Default,
            Mode = SqliteOpenMode.Memory
        };

        MemDbConnection = new SqliteConnection(conn_builder.ToString());
        MemDbConnection.Open();
        var sql_cmd = MemDbConnection.CreateCommand();
        sql_cmd.CommandText = SQL_DB_TEXT;
        sql_cmd.ExecuteNonQuery();

        var chunk_base_path = GetChunkBasePath();
        var output_base_path = GetOutputBasePath();
        try {
            io.Directory.Delete(chunk_base_path, true);
            io.Directory.Delete(output_base_path, true);
        } catch (io.DirectoryNotFoundException) {}
        io.Directory.CreateDirectory(chunk_base_path);
        io.Directory.CreateDirectory(output_base_path);
    }

    internal static StorageHandle CreateStorageHandle(StorageType storageType, int expireInSeconds = 30)
    {
        var id = Guid.NewGuid();
        var full_path = GenStoragePath(storageType, id);
        return new StorageHandle {
            Id = id,
            FullPath = full_path,
            TimeToExpireUtc = DateTime.UtcNow.AddSeconds(expireInSeconds)
        };
    }

    private static string GenStoragePath(StorageType storageType, Guid id) => storageType switch {
        StorageType.Chunk  => io.Path.Combine(GetChunkBasePath(), id.ToString()),
        StorageType.Output => io.Path.Combine(GetOutputBasePath(), id.ToString()),
        _                  => throw new ArgumentOutOfRangeException(nameof(storageType), storageType, null)
    };

    internal static StorageHandle? MaybeGetStorageHandle(Guid id, StorageType storageType, int addRetentionTime = 5)
    {
        var full_path = GenStoragePath(storageType, id);
        var maybe_expiration =
            MemDbConnection.QuerySingleOrDefault<DateTime?>(SELECT_TIME_TO_EXPIRE, new {Path = full_path});
        
        //if either the row was already deleted or the service has yet to get to it, tell the caller it doesn't exist
        if (maybe_expiration == null) return null;
        if (maybe_expiration < DateTime.UtcNow) return null;
        
        if (!io.File.Exists(full_path)) throw new io.FileNotFoundException(full_path);
        
        var storage_handle = new StorageHandle {
            Id = id,
            FullPath = full_path,
            TimeToExpireUtc = maybe_expiration.Value.AddSeconds(addRetentionTime)
        };
        EnqueueHandleDeletion(storage_handle);
        return storage_handle;
    }
    internal static void EnqueueHandleDeletion(StorageHandle storageHandle)
    {
        if (!io.File.Exists(storageHandle.FullPath))
            throw new ArgumentException($"expected FullPath '{storageHandle.FullPath}' to exist");
        var local_time = storageHandle.TimeToExpireUtc.ToLocalTime();

        var sql_cmd = MemDbConnection.CreateCommand();
        sql_cmd.CommandText = UPSERT_CLEANUP_ITEM;
        sql_cmd.AddParameterWithValue("@Path", storageHandle.FullPath);
        sql_cmd.AddParameterWithValue("@TimeToExpire", storageHandle.TimeToExpireUtc);
        Console.WriteLine($"inserting into FileCleanupQueue {{ path = '{storageHandle.FullPath}', ttl = '{local_time}'");

        if (sql_cmd.ExecuteNonQuery() != 1) {
            throw new Exception($"Unable to insert into FileCleanupQueue");
        }
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested) {
            var expire_start_utc = DateTime.UtcNow.AddSeconds(-30);
            Console.WriteLine("polling for cleanup items");
            var file_items = MemDbConnection.Query<string>(SELECT_EXPIRED_ITEMS, new {CurrentDateTimeUtc = expire_start_utc });
            if (file_items is not null) {
                foreach (var file_path in file_items) {
                    io.File.Delete(file_path);
                    Console.WriteLine($"deleting file '{file_path}'");
                }
                
                var sql_cmd = MemDbConnection.CreateCommand();
                sql_cmd.CommandText = DELETE_CLEANUP_ITEMS;
                sql_cmd.AddParameterWithValue("@CurrentDateTimeUtc", expire_start_utc);
                sql_cmd.ExecuteNonQuery();
            }
            await Task.Delay(1000 * 15, stoppingToken);
        }
    }
}