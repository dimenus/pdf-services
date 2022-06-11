using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Text.Json.Serialization;

namespace PdfServices.Service.DTO;

#pragma warning disable CS8618
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public class PdfInputResponseDto
{
    public string Uri { get; init; }

    public struct ItemChunk
    {
        public string Uri { get; init; }
        public long SizeInBytes { get; init; }
    }
        
    public string Sha256 { get; init; }
        
    public List<ItemChunk> ChunkList { get; init; }
}

#pragma warning restore CS8618