using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using SQLitePCL;
using StackExchange.Redis;

namespace PdfServices.Service.Services;

internal class RedisCacheService : BackgroundService
{
    internal readonly struct CacheItemHeader
    {
        public Guid Id { get; init; }
        public int SizeInBytes { get; init; }
        public string Sha256 { get; init; }

        public CacheInfo ToCacheInfo(DateTime expireDateTime)
        {
            return new CacheInfo {
                Id = Id,
                Sha256 = Sha256,
                SizeInBytes = SizeInBytes,
                ExpireDateTimeUtc = expireDateTime,
            };
        }
    }

    internal readonly struct CacheItemHash
    {
        public Guid HeaderId { get; init; }
    }

    internal readonly struct CacheInfo
    {
        public Guid Id { get; init; }
        public int SizeInBytes { get; init; }
        public string Sha256 { get; init; }
        public DateTime ExpireDateTimeUtc { get; init; }
    }
    
    private const int DEFAULT_EXPIRE_IN_MINUTES = 5;
    private const string KEY_NAMESPACE = "kw-pdf-services";

    private static readonly ConnectionMultiplexer RedisConnection;

    static RedisCacheService()
    {
        RedisConnection = ConnectionMultiplexer.Connect("localhost");
    }

    private static string GenerateDataKey(Guid? existingGuid = null) =>
        $"{KEY_NAMESPACE}/{existingGuid ?? Guid.NewGuid()}/data";
    
    private static string GenerateHeaderKey(Guid? existingGuid = null) =>
        $"{KEY_NAMESPACE}/{existingGuid ?? Guid.NewGuid()}/header";
    
    private static string GenerateHashKey(string sha256) =>
        $"{KEY_NAMESPACE}/{sha256}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested) {
            if (!RedisConnection.IsConnected) throw new Exception("Unable to communicate with Redis");
            
            await Task.Delay(1000 * 5, stoppingToken);
        }
        
    }

    internal static async Task<CacheInfo> AddCacheItemAsync(Guid id, string sha256, byte[] fileBytes, int expireInMinutes = DEFAULT_EXPIRE_IN_MINUTES)
    {
        var stopwatch = Stopwatch.StartNew();
        var db = RedisConnection.GetDatabase();
        var expiry_datetime = TimeSpan.FromMinutes(expireInMinutes);

        var hash_key = GenerateHashKey(sha256);
        var hash_bytes = JsonSerializer.SerializeToUtf8Bytes(new CacheItemHash {
            HeaderId = id
        });

        var res = await db.StringSetAsync(hash_key, hash_bytes, expiry_datetime);
        if (!res) {
            throw new Exception($"Unable to insert hash into cache");
        }
        var header_key = GenerateHeaderKey(id);
        var header_bytes = JsonSerializer.SerializeToUtf8Bytes(new CacheItemHeader {
            Id = id,
            Sha256 = sha256,
            SizeInBytes = fileBytes.Length
        });
        
        res = await db.StringSetAsync(header_key, header_bytes, expiry_datetime);
        if (!res) {
            throw new Exception($"Unable to insert header into cache");
        }

        var data_key = GenerateDataKey(id);
        res = await db.StringSetAsync(data_key, fileBytes, expiry_datetime);
        if (!res) {
            throw new Exception($"Unable to insert data into cache");
        }

        var cache_info = MaybeRefreshCacheInfo(id) ?? throw new Exception("expected a valid cache");
        Console.WriteLine($"elapsed timespan {stopwatch.Elapsed}");
        return cache_info;
    }

    internal static CacheInfo? MaybeRefreshCacheInfo(Guid id)
    {
        var stopwatch = Stopwatch.StartNew();
        var header_key = GenerateHeaderKey(id);
        var db = RedisConnection.GetDatabase();
        var default_expiry_timespan = TimeSpan.FromMinutes(DEFAULT_EXPIRE_IN_MINUTES);
        var expire_datetime = DateTime.UtcNow + default_expiry_timespan;
        
        if (!db.KeyExpire(GenerateDataKey(id), default_expiry_timespan)) return null;
        if (!db.KeyExpire(GenerateHeaderKey(id), default_expiry_timespan)) return null;
        
        var maybe_redis_value = db.StringGet(header_key);
        if (!maybe_redis_value.HasValue) throw new Exception("Header should not be null. We just refreshed");
        var redis_data = (byte[]?)maybe_redis_value;
        var cache_header = JsonSerializer.Deserialize<CacheItemHeader>(redis_data);
        
        var hash_key = GenerateHashKey(cache_header.Sha256);
        var hash_bytes = JsonSerializer.SerializeToUtf8Bytes(new CacheItemHash {
            HeaderId = id
        });

        var res = db.StringSet(hash_key, hash_bytes, default_expiry_timespan);
        if (!res) {
            throw new Exception($"Unable to insert hash into cache");
        }
        
        Console.WriteLine($"{nameof(MaybeRefreshCacheInfo)}: elapsed timespan {stopwatch.Elapsed}");
        return cache_header.ToCacheInfo(expire_datetime);
    }

    internal static CacheInfo? MaybeRefreshCacheInfo(string sha256)
    {
        var db = RedisConnection.GetDatabase();

        var hash_key = GenerateHashKey(sha256);
        var maybe_redis_value = db.StringGetWithExpiry(hash_key);
        if (!maybe_redis_value.Value.HasValue) return null;
        if (maybe_redis_value.Expiry!.Value < TimeSpan.FromSeconds(5)) return null;

        var expiry_timespan = TimeSpan.FromMinutes(DEFAULT_EXPIRE_IN_MINUTES);
        var maybe_cache_hash = db.StringGetSetExpiry(GenerateHashKey(sha256), expiry_timespan);
        
        if (!maybe_cache_hash.HasValue) return null;

        var hash_info = JsonSerializer.Deserialize<CacheItemHash>((byte[]?) maybe_cache_hash);

        return MaybeRefreshCacheInfo(hash_info.HeaderId);
    }

    internal static async Task CopyCacheDataToDisk(Guid fileId, string outputPath)
    {
        var db = RedisConnection.GetDatabase();
        
        var stopwatch = Stopwatch.StartNew();
        var data_key = GenerateDataKey(fileId);

        var redis_buffer = (ReadOnlyMemory<byte>) db.StringGet(data_key);
        await using var file_stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        await file_stream.WriteAsync(redis_buffer);
    }
}