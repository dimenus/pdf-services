using System;
using System.Collections.Generic;
using System.Text;
using PdfServices.Service.DTO;
using PdfServices.Service.Models;
using PdfServices.Service.Utils;

namespace PdfServices.Service;

public static class PdfInputProcessor
{
    private const int MAX_FILE_PART_SIZE = 1024 * 1024 * 2;

    public static PdfInputResponseDto Generate(string baseUri, SqliteDbContext dbContext, PdfInputRequestDto pdfInputRequest)
    {
        // ReSharper disable once MergeIntoLogicalPattern
        if (pdfInputRequest.SizeInBytes <= 0 || pdfInputRequest.SizeInBytes > int.MaxValue) throw new Exception("invalid filesize");
        if (pdfInputRequest.Sha256.Length != 64) throw new Exception("invalid hash (sha256 base16)");

        var list_chunks = new List<PdfInputResponseDto.ItemChunk>(128);
        var leftover_bytes = pdfInputRequest.SizeInBytes;
        var chunk_index = 0;

        var external_id = Guid.NewGuid().ToString();
        PdfInputModel.Create(dbContext, new PdfInputModel.InputInfo {
            ExternalId = external_id,
            Sha256Hash = pdfInputRequest.Sha256
        });

        var pdf_input = PdfInputModel.GetByExternalId(dbContext, external_id) ?? 
            throw new Exception($"No PdfInput was found for ExternalId '{external_id}'");

        var sb_uri = new StringBuilder(255);
        sb_uri.Append(baseUri);
        sb_uri.Append('/');
        sb_uri.Append(external_id);
        
        while (leftover_bytes > 0) {
            var chunk_file_size = Math.Min(leftover_bytes, MAX_FILE_PART_SIZE);
            leftover_bytes -= chunk_file_size;
            
            PdfInputChunkModel.Create(dbContext, pdf_input.Id, new PdfInputChunkModel.ChunkInfo {
                ChunkIndex = chunk_index,
                FileSizeInBytes = chunk_file_size
            });
            
            list_chunks.Add(new PdfInputResponseDto.ItemChunk {
                Uri = string.Concat(sb_uri.ToString(), "/", chunk_index, "/data"),
                SizeInBytes = chunk_file_size
            });
            chunk_index += 1;
        }

        return new PdfInputResponseDto {
            Uri = sb_uri.ToString(),
            Sha256 = pdfInputRequest.Sha256,
            ChunkList = list_chunks
        };
    }
    
}