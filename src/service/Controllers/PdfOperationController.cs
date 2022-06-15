using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PdfServices.Lib;
using PdfServices.Service.DTO;
using PdfServices.Service.Services;
using io = System.IO;

namespace PdfServices.Service.Controllers;

[ApiController]
[Route("[controller]")]
public class OperationsController : ControllerBase
{
    private readonly ZMuPdfLib _pdfLib;

    public OperationsController(ZMuPdfLib pdfLib)
    {
        _pdfLib = pdfLib;
    }

    [HttpPost("select")]
    public async Task<IActionResult> SelectIntoNewPdf([FromBody] List<SelectRequestDto> mergeItems)
    {
        var stopwatch = Stopwatch.StartNew();
        var list_pdf_selections = new List<PdfOperations.PdfSelection>(mergeItems.Count);
        try {
            foreach (var merge_dto in mergeItems) {
                var maybe_cache_info = RedisCacheService.MaybeRefreshCacheInfo(merge_dto.FileId);
                if (maybe_cache_info is null) return BadRequest();

                if (merge_dto.PageOffset is not null && merge_dto.PageOffset < 0)
                    return BadRequest("pageOffset was out of range");

                var temp_file_handle = LocalStorageService.CreateUnmanagedCacheItem();
                await RedisCacheService.CopyCacheDataToDisk(maybe_cache_info.Value.Id, temp_file_handle.FilePath);

                list_pdf_selections.Add(new PdfOperations.PdfSelection(temp_file_handle, merge_dto.PageOffset,
                    merge_dto.Length));
            }

            var output_storage_handle = PdfOperations.ProcessSelections(_pdfLib, list_pdf_selections);
            LocalStorageService.ConvertToManaged(output_storage_handle, 5);
            Console.WriteLine($"{nameof(SelectIntoNewPdf)} execution time '{stopwatch.Elapsed}'");
            return new PhysicalFileResult(output_storage_handle.FilePath, "application/pdf");
            
        } catch (ZMuPdfLibException ex) {
            switch (ex.CodeType) {
                case ZMuPdfLibException.ErrorCodeType.User:
                    return BadRequest(ex.Message);
                case ZMuPdfLibException.ErrorCodeType.ApiUsage:
                case ZMuPdfLibException.ErrorCodeType.Fatal:
                default:
                    throw;
            }
        }
        finally {
            foreach (var pdf_section in list_pdf_selections) {
                LocalStorageService.ConvertToManaged(pdf_section.CacheItem, 0);
            }
        }
    }

    public class OptionsHeaders
    {
        [FromHeader(Name = "Kw-Storage-Sha256")]
        public string? Sha256 { get; init; }
    }
}