using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace PdfServices.Service.DTO;

#pragma warning disable CS8618

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
[SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
public class PdfInputRequestDto
{
    [Required]
    public long SizeInBytes { get; init; }
        
    [Required]
    public string Sha256 { get; init; }
}
#pragma warning restore CS8618