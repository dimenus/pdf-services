using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using PdfServices.Service.DTO;

namespace PdfServices.Tests;

[SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
public class PdfServiceClient
{
    private const string HEALTH_ENDPOINT = "/health";
    private const string PDF_INPUT_ENDPOINT = "/PdfInput";
    private readonly HttpClient _httpClient;
    private readonly int _maxFileSizeInBytes;

#pragma warning disable CS8618
    
#pragma warning restore CS8618
    
    public PdfServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        var server_limits = httpClient.GetFromJsonAsync<ServerLimitsDtoV1>("PdfFile/server_limits").Result ??
            throw new Exception("Unable to retrieve server limits");
        _maxFileSizeInBytes = server_limits.MaxFileSizeInBytes;
    }

    public async Task<FileInfoResponseDtoV1> UploadPdf(string filePath)
    {
        var file_info = new FileInfo(filePath);
        if (!file_info.Exists) throw new FileNotFoundException(filePath);
        await using var file_stream = new FileStream(file_info.FullName, FileMode.Open, FileAccess.Read);
        var byte_buffer = ArrayPool<byte>.Shared.Rent((int)file_info.Length);
        try {
            var num_bytes_read = file_stream.Read(byte_buffer.AsSpan());
            if (num_bytes_read != file_info.Length) {
                throw new Exception(
                    $"expected FileStream position to be ({file_info.Length}) but instead got ({file_stream.Position})");
            }
            using var hash = SHA256.Create();
            var raw_hash = hash.ComputeHash(byte_buffer[0..num_bytes_read]);
            var sb_hash = new StringBuilder(raw_hash.Length * 2);
            foreach (var b in raw_hash) sb_hash.Append(b.ToString("X2"));
            
            var byte_array_content = new ByteArrayContent(byte_buffer, 0, (int)file_info.Length);
            var result = await _httpClient.PostAsync("/PdfFile", byte_array_content);
            
            if (!result.IsSuccessStatusCode) {
                var resp_data = result.Content.ReadAsStringAsync().Result;
                throw new Exception(resp_data);
            }
            
            var chunk_info = await result.Content.ReadFromJsonAsync<FileInfoResponseDtoV1>() ??
                             throw new Exception("Chunk response was null");
            Console.WriteLine(chunk_info.Id);

            return chunk_info;
            
        } finally {
            ArrayPool<byte>.Shared.Return(byte_buffer);
        }
     }

    public async Task<byte[]> GetPdfPage(Guid fileId, int pageNumber)
    {
        var list_select_dtos = new List<SelectRequestDtoV1>(1) {
            new SelectRequestDtoV1 {
                FileId = fileId,
                PageOffset = pageNumber - 1,
                Length = 1
            }
        };

        var response = await _httpClient.PostAsJsonAsync("/Operations/select", list_select_dtos);
        if (!response.IsSuccessStatusCode) throw new Exception(response.ReasonPhrase);

        return await response.Content.ReadAsByteArrayAsync();
    }
}