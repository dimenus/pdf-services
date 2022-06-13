using System;
using System.Diagnostics.CodeAnalysis;

namespace PdfServices.Service.DTO;

#pragma warning disable CS8618
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]

public class ChunkInfoResponseDto
{
    public string Id { get; init; }
    public long SizeInBytes { get; init; }
    public string Sha256 { get; init; }
    
    public DateTime TimeToExpireUtc { get; init; }
    
}

#pragma warning restore CS8618