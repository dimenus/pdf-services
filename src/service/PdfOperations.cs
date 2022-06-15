using System;
using io = System.IO;
using System.Buffers;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using PdfServices.Lib;
using PdfServices.Service.DTO;
using PdfServices.Service.Services;

namespace PdfServices.Service;

public static class PdfOperations
{
    private const int READ_BUFFER_SIZE = 1024 * 1024;
    
    internal struct PdfSelection
    {
        internal PdfSelection(LocalStorageService.UnmanagedCacheItem cacheItem, int? pageOffset, int? length)
        {
            CacheItem = cacheItem;
            PageOffset = pageOffset;
            Length = length;
        }

        public readonly LocalStorageService.UnmanagedCacheItem CacheItem;
        public readonly int? PageOffset;
        public readonly int? Length;
    }

    internal class BufferHandle : IDisposable
    {
        private int _bufferLength;
        private IMemoryOwner<byte>? _memoryOwner;

        public BufferHandle(int sizeInBytes)
        {
            _memoryOwner = MemoryPool<byte>.Shared.Rent(sizeInBytes);
        }
        
        public Memory<byte> RawMemory => _memoryOwner?.Memory ?? throw new ObjectDisposedException(nameof(BufferHandle));

        public void SetBufferLength(int length)
        {
            if (_memoryOwner == null) throw new ObjectDisposedException(nameof(BufferHandle));
            if (length > _memoryOwner.Memory.Length) 
                throw new ArgumentOutOfRangeException(nameof(length));
            
            _bufferLength = length;
        }

        public int GetBufferLength() => 
            _memoryOwner != null ? _bufferLength : throw new ObjectDisposedException(nameof(BufferHandle));

        public Memory<byte> GetUsedMemory()
        {
            if (_memoryOwner == null) throw new ObjectDisposedException(nameof(BufferHandle));
            return _memoryOwner.Memory[.._bufferLength];
        }

        public Span<byte> GetUsedSpan()
        {
            if (_memoryOwner == null) throw new ObjectDisposedException(nameof(BufferHandle));
            return _memoryOwner.Memory.Span[_bufferLength..];
        }

        public void Dispose()
        {
            _bufferLength = 0;
            _memoryOwner?.Dispose();
            _memoryOwner = null;
        }
    }

    internal static LocalStorageService.UnmanagedCacheItem ProcessSelections(ZMuPdfLib pdfLib, PdfSelection pdfSelection) =>
        ProcessSelections(pdfLib, new List<PdfSelection>(1) {pdfSelection});

    internal static LocalStorageService.UnmanagedCacheItem ProcessSelections(ZMuPdfLib pdfLib,
        List<PdfSelection> pdfSelections)
    {
        var list_lib_handles = new List<uint>(pdfSelections.Count);
        var output_temp_file = LocalStorageService.CreateUnmanagedCacheItem();
        pdfLib.OpenOutput();
        try {
            foreach (var pdf_selection in pdfSelections) {
                Console.WriteLine($"{nameof(ZMuPdfLib)} opening path '{pdf_selection.CacheItem.FilePath}");
                var lib_handle = pdfLib.OpenInput(pdf_selection.CacheItem.FilePath);
                list_lib_handles.Add(lib_handle);

                var page_offset = pdf_selection.PageOffset ?? 0;
                var actual_num_pages = pdfLib.GetInputPageCount(lib_handle);
                var num_pages = pdf_selection.Length ?? actual_num_pages - page_offset;
                if (page_offset < 0) throw new ArgumentOutOfRangeException(nameof(page_offset));

                pdfLib.CopyPagesToOutput(lib_handle, page_offset, num_pages);
                pdfLib.DropInput(lib_handle);
                list_lib_handles.Remove(lib_handle);
                LocalStorageService.ConvertToManaged(pdf_selection.CacheItem, 0);
            }
        }
        finally {
            foreach (var item in list_lib_handles) pdfLib.DropInput(item);
            pdfLib.SaveOutput(output_temp_file.FilePath);
            pdfLib.DropOutput();
        }

        return output_temp_file;
    }

    private static string GenerateSha256(Span<byte> sourceBuffer)
    {
        var sb_hash = new StringBuilder(64);
        var raw_hash = SHA256.HashData(sourceBuffer);
        foreach (var b in raw_hash) sb_hash.Append(b.ToString("X2"));
        return sb_hash.ToString();
    }

    internal static async Task<ChunkInfoResponseDto> ProcessFileAsync(ZMuPdfLib pdfLib, io.Stream chunkStream, long expectedChunkSize,
        string? maybeSha256Hash)
    {
        var raw_file_buffer = ArrayPool<byte>.Shared.Rent((int)expectedChunkSize);
        var read_buffer = ArrayPool<byte>.Shared.Rent(READ_BUFFER_SIZE);
        var hash_stream = new System.IO.MemoryStream(raw_file_buffer);
        var sb_hash = new StringBuilder(128);

        var temp_cache_item = LocalStorageService.CreateUnmanagedCacheItem();
        pdfLib.OpenOutput();
        uint? libpdf_input_handle = null;
        try {
            var num_bytes_read = 0;
            
            await using var file_stream = new io.FileStream(temp_cache_item.FilePath, io.FileMode.CreateNew);
            while (true) {
                var current_bytes_read = await chunkStream.ReadAsync(read_buffer, 0, read_buffer.Length);
                if (current_bytes_read == 0) break;
                await hash_stream.WriteAsync(read_buffer, 0, current_bytes_read);
                await file_stream.WriteAsync(read_buffer, 0, current_bytes_read);
                num_bytes_read += current_bytes_read;
            }
            
            file_stream.Flush();
            libpdf_input_handle = pdfLib.OpenInput(temp_cache_item.FilePath);
            var used_buffer = raw_file_buffer[..num_bytes_read];
            
            var output_hash = GenerateSha256(used_buffer);
            
            if (maybeSha256Hash is not null && output_hash != maybeSha256Hash)
                throw new ArgumentException($"expected Sha256 '{maybeSha256Hash}' but got '{output_hash}' instead");

            var cache_info = RedisCacheService.MaybeRefreshCacheInfo(output_hash);
            var cache_id = cache_info?.Id ?? Guid.NewGuid();
            cache_info ??= await RedisCacheService.AddCacheItemAsync(cache_id, output_hash, used_buffer);

            return new ChunkInfoResponseDto {
                Id = cache_id,
                SizeInBytes = expectedChunkSize,
                Sha256 = sb_hash.ToString(),
                ExpireDateTimeUtc = cache_info.Value.ExpireDateTimeUtc
            };
        }
        finally {
            if (libpdf_input_handle is not null) pdfLib.DropInput(libpdf_input_handle.Value);
            pdfLib.DropOutput();
            LocalStorageService.ConvertToManaged(temp_cache_item, 0);
            ArrayPool<byte>.Shared.Return(read_buffer);
            ArrayPool<byte>.Shared.Return(raw_file_buffer);
        }
    }
    
    internal static ChunkInfoResponseDto? MaybeGetFileInfo(Guid id)
    {
        var maybe_redis_handle = RedisCacheService.MaybeRefreshCacheInfo(id);
        if (maybe_redis_handle is null) return null;

        return new ChunkInfoResponseDto {
            Id = maybe_redis_handle.Value.Id,
            Sha256 = maybe_redis_handle.Value.Sha256,
            SizeInBytes = maybe_redis_handle.Value.SizeInBytes,
            ExpireDateTimeUtc = maybe_redis_handle.Value.ExpireDateTimeUtc
        };
    }
}