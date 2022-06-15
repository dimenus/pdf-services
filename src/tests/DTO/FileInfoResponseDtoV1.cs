using System;
using System.Diagnostics.CodeAnalysis;

namespace PdfServices.Service.DTO;

#pragma warning disable CS8618
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]

public class FileInfoResponseDtoV1
{
    public Guid Id { get; init; }
    public long SizeInBytes { get; init; }
    public string Sha256 { get; init; }
    public DateTime ExpireDateTimeUtc { get; init; }
    
}

#pragma warning restore CS8618