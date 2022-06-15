using System;
using System.Diagnostics;
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
    
    private const string SELECT_EXPIRED_ITEMS =
        "SELECT Path FROM FileCleanupQueue WHERE TimeToExpire <= @CurrentDateTimeUtc";

    private const string DELETE_CLEANUP_ITEMS = "DELETE FROM FileCleanupQueue WHERE TimeToExpire <= @CurrentDateTimeUtc";
    
    private static readonly SqliteConnection MemDbConnection;

    private static readonly string StorageBasePath;
    
    internal struct UnmanagedCacheItem
    {
        public UnmanagedCacheItem(string filePath)
        {
            FilePath = filePath;
        }

        public readonly string FilePath;
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
        
        StorageBasePath = io.Path.Combine(io.Path.GetTempPath(), "kwapi-pdf-services", Process.GetCurrentProcess().Id.ToString());
        io.Directory.CreateDirectory(StorageBasePath);
    }
    
    internal static void AddManagedCacheItem(Guid id, Span<byte> fileBytes, int expireInSeconds = 180)
    {
        var full_path = GenStoragePath(id);
        io.Directory.CreateDirectory(StorageBasePath);
        using var fs = new io.FileStream(full_path, io.FileMode.Create);
        fs.Write(fileBytes);
        fs.Close();
        
        EnqueueHandleDeletion(full_path, DateTime.UtcNow.AddSeconds(expireInSeconds));
    }

    internal static UnmanagedCacheItem CreateUnmanagedCacheItem()
    {
        return new UnmanagedCacheItem(GenStoragePath(Guid.NewGuid()));
    }

    internal static void ConvertToManaged(UnmanagedCacheItem item, int expireInSeconds = 30)
    {
        var ttl = DateTime.UtcNow.AddSeconds(expireInSeconds);
        EnqueueHandleDeletion(item.FilePath, ttl);
    }

    private static string GenStoragePath(Guid id) =>
        io.Path.Combine(StorageBasePath, id.ToString());

    private static void EnqueueHandleDeletion(string filePath, DateTime expireDateTimeUtc)
    {
        var sql_cmd = MemDbConnection.CreateCommand();
        sql_cmd.CommandText = UPSERT_CLEANUP_ITEM;
        sql_cmd.AddParameterWithValue("@Path", filePath);
        sql_cmd.AddParameterWithValue("@TimeToExpire", expireDateTimeUtc);

        if (sql_cmd.ExecuteNonQuery() != 1) {
            throw new Exception($"Unable to insert into FileCleanupQueue");
        }
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested) {
            var expire_start_utc = DateTime.UtcNow;
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