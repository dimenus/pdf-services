using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PdfServices.Tests;

[SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
public class PdfServiceClient
{
    private const string HEALTH_ENDPOINT = "/health";
    private const string PDF_INPUT_ENDPOINT = "/PdfInput";
    private readonly HttpClient _httpClient;

    #pragma warning disable CS8618
    public class PdfInputPayload
    {
        [JsonPropertyName("sizeInBytes")]
        public int SizeInBytes { get; init; }
        
        [JsonPropertyName("sha256")]
        public string Sha256 { get; init; }
    }
    
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
    public class PdfInputResponse
    {
        public string Uri { get; init; }

        public struct ItemChunk
        {
            public string Uri { get; init; }
            public int SizeInBytes { get; init; }
        }
        
        public string Sha256 { get; init; }
        
        public List<ItemChunk> ChunkList { get; init; }
    }

#pragma warning disable CS8618

    public PdfServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public void UploadPdf(string filePath)
    {
        var file_info = new FileInfo(filePath);
        if (!file_info.Exists) throw new FileNotFoundException(filePath);
        using var file_stream = new FileStream(file_info.FullName, FileMode.Open, FileAccess.Read);
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
            var input_payload = new PdfInputPayload {
                Sha256 = sb_hash.ToString(),
                SizeInBytes = (int) file_info.Length
            };
            var response = _httpClient.PostAsJsonAsync(PDF_INPUT_ENDPOINT, input_payload).Result;
            if (!response.IsSuccessStatusCode) {
                var raw_response_data = response.Content.ReadAsStringAsync().Result;
                throw new Exception(raw_response_data);
            }
            var response_payload = response.Content.ReadFromJsonAsync<PdfInputResponse>().Result ?? 
                                 throw new Exception($"PdfInput response was unexpectedly null");
            
            var base_byte_index = 0;
            var list_tasks = new List<Task<HttpResponseMessage>>(response_payload.ChunkList.Count);
            foreach (var chunk in response_payload.ChunkList) {
                list_tasks.Add(_httpClient.PutAsync($"{chunk.Uri}",
                    new ByteArrayContent(byte_buffer, base_byte_index, chunk.SizeInBytes)));
                base_byte_index += chunk.SizeInBytes;
            }
            var results = Task.WhenAll(list_tasks).Result;
            foreach (var item in results) {
                if (!item.IsSuccessStatusCode) {
                    var resp_data = item.Content.ReadAsStringAsync().Result;
                    throw new Exception(resp_data);
                }
            }
        } finally {
            ArrayPool<byte>.Shared.Return(byte_buffer);
        }
     }
}