using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Text.Json.Serialization;

namespace PdfServices.Service;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public class PdfInputItem
{
    [Required]
    [JsonPropertyName("size")]
    public int Size { get; init; }

    [Required] [JsonPropertyName("hash")] 
    public string Hash { get; init; } = null!;
}
