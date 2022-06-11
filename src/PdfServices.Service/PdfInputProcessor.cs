using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PdfServices.Service.DTO;
using PdfServices.Service.Models;
using PdfServices.Service.Utils;

namespace PdfServices.Service;

public static class PdfInputProcessor
{
    private const int MAX_FILE_PART_SIZE = 1024 * 1024 * 8;

    public static PdfInputResponseDto Generate(string baseUri, SqliteDbContext dbContext, PdfInputRequestDto pdfInputRequest)
    {
        // ReSharper disable once MergeIntoLogicalPattern
        if (pdfInputRequest.SizeInBytes <= 0 || pdfInputRequest.SizeInBytes > int.MaxValue) throw new Exception("invalid filesize");
        if (pdfInputRequest.Sha256.Length != 64) throw new Exception("invalid hash (sha256 base16)");

        var list_chunks = new List<PdfInputResponseDto.ItemChunk>(128);
        var leftover_bytes = pdfInputRequest.SizeInBytes;

        var external_id = Guid.NewGuid().ToString();
        PdfInputModel.Create(dbContext, new PdfInputModel.InputInfo {
            ExternalId = external_id,
            ExpectedFileSize = pdfInputRequest.SizeInBytes,
            Sha256Hash = pdfInputRequest.Sha256,
        });
        
        var storage_path = Path.Combine(Path.GetTempPath(), "kwapi-pdf-services", external_id);
        Directory.CreateDirectory(storage_path);

        var sb_uri = new StringBuilder(255);
        sb_uri.Append(baseUri);
        sb_uri.Append('/');
        sb_uri.Append(external_id);

        var chunk_index = 0;
        var list_chunk_sizes = CalculateChunkSizes(pdfInputRequest.SizeInBytes);
        foreach (var chunk_size in list_chunk_sizes) {
            list_chunks.Add(new PdfInputResponseDto.ItemChunk {
                Uri = string.Concat(sb_uri.ToString(), "/", chunk_index, "/data"),
                SizeInBytes = chunk_size
            });
            chunk_index += 1;
        }
        
        return new PdfInputResponseDto {
            Uri = sb_uri.ToString(),
            Sha256 = pdfInputRequest.Sha256,
            ChunkList = list_chunks
        };
    }

    public static List<long> CalculateChunkSizes(long totalFileSize)
    {
        var list_sizes = new List<long>((int)Math.Max((totalFileSize / MAX_FILE_PART_SIZE), 1) * 2);

        var leftover_bytes = totalFileSize;
        while (leftover_bytes > 0) {
            var chunk_file_size = Math.Min(leftover_bytes, MAX_FILE_PART_SIZE);
            leftover_bytes -= chunk_file_size;
            list_sizes.Add(chunk_file_size);
        }
        return list_sizes;
    }
    
}