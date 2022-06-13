using System;
using System.Buffers;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PdfServices.Lib;
using PdfServices.Service.DTO;
using PdfServices.Service.Services;
using io = System.IO;

namespace PdfServices.Service.Controllers;

[ApiController]
[Route("[controller]")]
public class PdfOpsController : ControllerBase
{
    private const int MAX_FILE_PART_SIZE = 1024 * 1024 * 16;
    private const int READ_BUFFER_SIZE = 4 * 1024 * 1024;
    private readonly ZMuPdfLib _pdfLib;

    public PdfOpsController(ZMuPdfLib pdfLib)
    {
        _pdfLib = pdfLib;
    }

    [HttpPost("chunk")]
    public async Task<ActionResult<ChunkInfoResponseDto>> PostChunk([FromHeader] OptionsHeaders opts)
    {
        if (Request.ContentLength is null) return BadRequest();
        var chunk_size = Request.ContentLength!.Value;
        if (chunk_size > GetChunkSizeLimit()) return BadRequest();

        return await ProcessChunkAsync(Request.Body, Request.ContentLength!.Value, opts.Sha256);
    }

    [HttpGet("chunk/{externalId:guid}")]
    public ActionResult<ChunkInfoResponseDto> GetChunkInfo(Guid externalId)
    {
        var chunk_info = MaybeGetChunkInfo(externalId.ToString());
        if (chunk_info is not null) return chunk_info;
        
        return NotFound();
    }

    [HttpPost("select")]
    public async Task<IActionResult> SelectIntoPdf([FromBody] List<MergeRequestDto> mergeItems)
    {
        var list_pdf_selections = new List<PdfSelection>(mergeItems.Count);
        foreach (var merge_dto in mergeItems) {
            var list_chunk_handles = new List<LocalStorageService.StorageHandle>(merge_dto.ChunkIds.Count);
            foreach (var chunk_id in merge_dto.ChunkIds) {
                var maybe_storage_handle = LocalStorageService.MaybeGetStorageHandle(Guid.ParseExact(chunk_id, "D"),
                    LocalStorageService.StorageType.Chunk);
                if (maybe_storage_handle == null) return BadRequest();
                list_chunk_handles.Add(maybe_storage_handle.Value);
            }

            if (merge_dto.PageOffset is not null && merge_dto.PageOffset < 0)
                return BadRequest("pageOffset was out of range");

            list_pdf_selections.Add(new PdfSelection(list_chunk_handles, merge_dto.PageOffset,
                merge_dto.Length));
        }

        try {
            var output_storage_handle = await SelectIntoOutputPdf(_pdfLib, list_pdf_selections);
            LocalStorageService.EnqueueHandleDeletion(output_storage_handle);
            return new PhysicalFileResult(output_storage_handle.FullPath, "application/pdf");
        } catch (ZMuPdfLibException ex) {
            switch (ex.CodeType) {
                case ZMuPdfLibException.ErrorCodeType.User:
                    return BadRequest(ex.Message);
                case ZMuPdfLibException.ErrorCodeType.ApiUsage:
                case ZMuPdfLibException.ErrorCodeType.Fatal:
                default:
                    throw;
            }
        }
    }

    private static long GetChunkSizeLimit()
    {
        return MAX_FILE_PART_SIZE;
    }

    private static async Task<LocalStorageService.StorageHandle> GeneratePdfOutputAsync(
        List<LocalStorageService.StorageHandle> chunkStorageHandles, string? maybeExpectedHashPdf = null)
    {
        var storage_handle = LocalStorageService.CreateStorageHandle(LocalStorageService.StorageType.Output);
        await using var output_file_stream = new io.FileStream(storage_handle.FullPath, io.FileMode.CreateNew);
        var read_buffer = ArrayPool<byte>.Shared.Rent(READ_BUFFER_SIZE);
        try {
            foreach (var chunk_handle in chunkStorageHandles) {
                await using var file_stream = new io.FileStream(chunk_handle.FullPath, io.FileMode.Open);
                var num_chunk_bytes_read = 0;
                while (true) {
                    var current_bytes_read = await file_stream.ReadAsync(read_buffer);
                    if (current_bytes_read == 0) break;

                    num_chunk_bytes_read += current_bytes_read;
                    output_file_stream.Write(read_buffer, 0, current_bytes_read);
                }

                if (num_chunk_bytes_read != file_stream.Length) throw new Exception("foo bar baz");
            }

            if (maybeExpectedHashPdf is not null) {
                output_file_stream.Seek(0, io.SeekOrigin.Begin);
                var hash_func = SHA256.Create();
                var raw_hash = await hash_func.ComputeHashAsync(output_file_stream);
                var sb_hash = new StringBuilder(raw_hash.Length * 2);
                foreach (var b in raw_hash) sb_hash.Append(b.ToString("X2"));

                var output_hash = sb_hash.ToString();
                if (maybeExpectedHashPdf != null && output_hash != maybeExpectedHashPdf)
                    throw new Exception("invalid hash");
            }
        } finally {
            await output_file_stream.DisposeAsync();
            ArrayPool<byte>.Shared.Return(read_buffer);
        }

        LocalStorageService.EnqueueHandleDeletion(storage_handle);
        return storage_handle;
    }

    private static async Task<ChunkInfoResponseDto> ProcessChunkAsync(io.Stream chunkStream, long expectedChunkSize,
        string? maybeSha256Hash, int expireInSeconds = 180)
    {
        if (expectedChunkSize > MAX_FILE_PART_SIZE)
            throw new ArgumentOutOfRangeException(
                $"chunk size ({expectedChunkSize}) is greater than the allowed limit");
        var storage_handle =
            LocalStorageService.CreateStorageHandle(LocalStorageService.StorageType.Chunk, expireInSeconds);
        var read_buffer = ArrayPool<byte>.Shared.Rent(READ_BUFFER_SIZE);
        var hash_buffer = ArrayPool<byte>.Shared.Rent(MAX_FILE_PART_SIZE);
        var hash_stream = new io.MemoryStream(hash_buffer);
        var sb_hash = new StringBuilder(128);
        await using var fs = new io.FileStream(storage_handle.FullPath, io.FileMode.Create);
        try {
            var num_bytes_read = 0;
            while (true) {
                var current_bytes_read = await chunkStream.ReadAsync(read_buffer, 0, read_buffer.Length);
                if (current_bytes_read == 0) break;
                await fs.WriteAsync(read_buffer, 0, current_bytes_read);
                await hash_stream.WriteAsync(read_buffer, 0, current_bytes_read);
                num_bytes_read += current_bytes_read;
            }

            await fs.DisposeAsync();
            using var hash_func = SHA256.Create();
            var raw_hash = hash_func.ComputeHash(hash_buffer[..(int) hash_stream.Position]);
            foreach (var b in raw_hash) sb_hash.Append(b.ToString("X2"));

            var output_hash = sb_hash.ToString();
            if (maybeSha256Hash is not null && output_hash != maybeSha256Hash)
                throw new ArgumentException($"expected Sha256 '{maybeSha256Hash}' but got '{output_hash}' instead");

            if (num_bytes_read != expectedChunkSize)
                throw new Exception(
                    $"expected to read ({expectedChunkSize}) bytes from the stream but got ({num_bytes_read})");
        } finally {
            ArrayPool<byte>.Shared.Return(read_buffer);
            ArrayPool<byte>.Shared.Return(hash_buffer);
        }

        LocalStorageService.EnqueueHandleDeletion(storage_handle);
        return new ChunkInfoResponseDto {
            Id = storage_handle.Id.ToString(),
            SizeInBytes = expectedChunkSize,
            Sha256 = sb_hash.ToString(),
            TimeToExpireUtc = storage_handle.TimeToExpireUtc
        };
    }

    private static ChunkInfoResponseDto? MaybeGetChunkInfo(string id)
    {
        var maybe_storage_handle =
            LocalStorageService.MaybeGetStorageHandle(Guid.ParseExact(id, "D"), LocalStorageService.StorageType.Chunk);
        if (maybe_storage_handle is null) return null;

        var storage_handle = maybe_storage_handle.Value;

        using var file_stream = new io.FileStream(storage_handle.FullPath, io.FileMode.Open, io.FileAccess.Read);

        var sb_hash = new StringBuilder(128);
        using var hash_func = SHA256.Create();
        var raw_hash = hash_func.ComputeHash(file_stream);
        foreach (var b in raw_hash) sb_hash.Append(b.ToString("X2"));
        var output_hash = sb_hash.ToString();

        return new ChunkInfoResponseDto {
            Id = id,
            Sha256 = output_hash,
            SizeInBytes = file_stream.Length,
            TimeToExpireUtc = storage_handle.TimeToExpireUtc
        };
    }

    private static async Task<LocalStorageService.StorageHandle> SelectIntoOutputPdf(ZMuPdfLib pdfLib,
        List<PdfSelection> pdfSelections, int expireInSeconds = 30)
    {
        var list_lib_handles = new List<uint>(pdfSelections.Count);
        var output_storage_handle =
            LocalStorageService.CreateStorageHandle(LocalStorageService.StorageType.Output, expireInSeconds);
        pdfLib.OpenOutput();
        try {
            foreach (var pdf_selection in pdfSelections) {
                var input_storage_handle = await GeneratePdfOutputAsync(pdf_selection.ChunkHandles);
                var lib_handle = pdfLib.OpenInput(input_storage_handle.FullPath);
                list_lib_handles.Add(lib_handle);

                var num_pages = pdf_selection.Length ?? pdfLib.GetInputPageCount(lib_handle);
                var page_offset = pdf_selection.PageOffset ?? 0;
                if (page_offset < 0) throw new ArgumentOutOfRangeException(nameof(page_offset));

                pdfLib.CopyPagesToOutput(lib_handle, page_offset, num_pages);
                pdfLib.DropInput(lib_handle);
                list_lib_handles.Remove(lib_handle);
            }
        } finally {
            foreach (var item in list_lib_handles) pdfLib.DropInput(item);
            pdfLib.SaveOutput(output_storage_handle.FullPath);
            pdfLib.DropOutput();
        }

        return output_storage_handle;
    }

    public class OptionsHeaders
    {
        [FromHeader(Name = "Kw-Storage-Sha256")]
        public string? Sha256 { get; init; }
    }

    private struct PdfSelection
    {
        public PdfSelection(List<LocalStorageService.StorageHandle> chunkHandles, int? pageOffset, int? length)
        {
            ChunkHandles = chunkHandles;
            PageOffset = pageOffset;
            Length = length;
        }

        public readonly List<LocalStorageService.StorageHandle> ChunkHandles;
        public readonly int? PageOffset;
        public readonly int? Length;
    }
}