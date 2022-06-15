using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PdfServices.Lib;
using PdfServices.Service.DTO;
using PdfServices.Service.Services;
using RedisCore;

namespace PdfServices.Service.Controllers;

[ApiController]
[Route("[controller]")]
public class PdfFileController : ControllerBase
{
    private const int MAX_FILE_SIZE = 1024 * 1024 * 32;
    private readonly ZMuPdfLib _pdfLib;

    public PdfFileController(ZMuPdfLib pdfLib)
    {
        _pdfLib = pdfLib;
    }

    [HttpGet("limits")]
    public ActionResult<ServerLimitsDto> GetServerLimits()
    {
        return new ServerLimitsDto {
            MaxFileSizeInBytes = MAX_FILE_SIZE
        };
    }
    
    [HttpPost]
    public async Task<ActionResult<ChunkInfoResponseDto>> CreatePdfFile([FromHeader] OperationsController.OptionsHeaders opts)
    {
        if (Request.ContentLength is null) return BadRequest();
        var chunk_size = Request.ContentLength!.Value;
        if (chunk_size > MAX_FILE_SIZE) return BadRequest();
        
        return await PdfOperations.ProcessFileAsync(_pdfLib, Request.Body, Request.ContentLength!.Value, opts.Sha256);
    }
    
    
    [HttpGet("{externalId:guid}/info")]
    public ActionResult<ChunkInfoResponseDto> GetChunkInfo(Guid externalId)
    {
        var chunk_info = PdfOperations.MaybeGetFileInfo(externalId);
        if (chunk_info is not null) return chunk_info;

        return NotFound();
    }

    [HttpGet("{externalId:guid}/data")]
    public async Task<ActionResult> GetData(Guid externalId, [FromQuery] int? pageOffset, int? length)
    {
        var chunk_info = PdfOperations.MaybeGetFileInfo(externalId);
        if (chunk_info is null) return NotFound();

        var source_item = LocalStorageService.CreateUnmanagedCacheItem();
        await RedisCacheService.CopyCacheDataToDisk(externalId, source_item.FilePath);
        if (pageOffset != null || length != null) {
            var pdf_selection = new PdfOperations.PdfSelection(source_item, pageOffset, length);
            var output_item = PdfOperations.ProcessSelections(_pdfLib, pdf_selection);
            LocalStorageService.ConvertToManaged(output_item, 5);
            return PhysicalFile(output_item.FilePath, "application/pdf");
        }
        
        
        LocalStorageService.ConvertToManaged(source_item, 5);
        return PhysicalFile(source_item.FilePath, "application/pdf");
    }
}