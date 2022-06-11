using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PdfServices.Service.DTO;
using PdfServices.Service.Models;
using PdfServices.Service.Utils;

namespace PdfServices.Service.Controllers;

[ApiController]
[Route("[controller]")]
public class PdfInputController : ControllerBase
{
    private readonly SqliteDbContext _dbContext;

    public PdfInputController(SqliteDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpPost]
    public ActionResult<PdfInputResponseDto> CreateInputPdf([FromBody] PdfInputRequestDto pdfInputRequest)
    {
        var base_uri = HttpContext.Request.Path.Value ??
                       throw new Exception("RequestPathValue was unexpectedly null");
        
        var resp = PdfInputProcessor.Generate(base_uri, _dbContext, pdfInputRequest);
        return Created(resp.Uri, resp);
    }

    [HttpPut("{externalId:guid}/{chunkIndex:int}/data")]
    public async Task<IActionResult> UploadChunkPayload(Guid externalId, long chunkIndex)
    {
        var pdf_input = PdfInputModel.GetByExternalId(_dbContext, externalId.ToString());
        if (pdf_input?.Id is null) return NotFound();
        
        var target_chunk = PdfInputChunkModel.Get(_dbContext, pdf_input.Id, chunkIndex);
        if (target_chunk == null) return BadRequest();

        if (Request.ContentLength != target_chunk.FileSizeInBytes) {
            return BadRequest();
        }

        var output_path = Path.Combine(pdf_input.LocalStoragePath, $"{chunkIndex}.pdf");
        var fs = new FileStream(output_path, FileMode.Create);
        var read_buffer = ArrayPool<byte>.Shared.Rent(65536);
        
        try {
            var num_bytes_read = 0;
            var body_stream = Request.BodyReader.AsStream();
            while (true) {
                var current_bytes_read = await body_stream.ReadAsync(read_buffer, 0, read_buffer.Length);
                if (current_bytes_read == 0) break;
                num_bytes_read += current_bytes_read;
                await fs.WriteAsync(read_buffer, 0, current_bytes_read);
            }

            if (num_bytes_read != target_chunk.FileSizeInBytes) {
                throw new Exception(
                    $"expected to read ({target_chunk.FileSizeInBytes}) bytes but instead got ({num_bytes_read})");
            }
            
            PdfInputChunkModel.UpdateStatus(_dbContext, pdf_input.Id, chunkIndex, PdfInputChunkModel.ChunkStatus.StoredLocally);
        } finally {
            ArrayPool<byte>.Shared.Return(read_buffer);
            fs.Close();
            Console.WriteLine($"saved file => '{output_path}'");   
        }
        return Ok();
    }
}