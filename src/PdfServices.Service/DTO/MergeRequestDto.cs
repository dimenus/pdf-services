using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace PdfServices.Service.DTO;

#pragma warning disable CS8618

public class MergeRequestDto
{
    [Required]
    [JsonPropertyName("chunkIds")]
    public List<string> ChunkIds { get; init; }
    
    [JsonPropertyName("pageOffset")]
    public int? PageOffset { get; init; }
    
    [JsonPropertyName("length")]
    public int? Length { get; init; }
}
#pragma warning restore CS8618