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
    public async Task<IActionResult> UploadChunkPayload(Guid externalId, int chunkIndex)
    {
        var pdf_input = PdfInputModel.GetByExternalId(_dbContext, externalId.ToString());
        if (pdf_input?.Id is null) return NotFound();

        var list_chunk_sizes = PdfInputProcessor.CalculateChunkSizes(pdf_input.ExpectedFileSize);
        if (chunkIndex >= list_chunk_sizes.Count) return BadRequest();

        var target_chunk_size = list_chunk_sizes[chunkIndex];
        
        if (Request.ContentLength != target_chunk_size) {
            return BadRequest();
        }
        
        var output_path = Path.Combine(pdf_input.GetLocalStoragePath(), $"{chunkIndex}.chunk");
        var fs = new FileStream(output_path, FileMode.Create);
        var read_buffer = ArrayPool<byte>.Shared.Rent(65536);

        var is_valid = false;
        try {
            var num_bytes_read = 0;
            var body_stream = Request.BodyReader.AsStream();
            while (true) {
                var current_bytes_read = await body_stream.ReadAsync(read_buffer, 0, read_buffer.Length);
                if (current_bytes_read == 0) break;
                num_bytes_read += current_bytes_read;
                await fs.WriteAsync(read_buffer, 0, current_bytes_read);
            }
            
            if (num_bytes_read != target_chunk_size) {
                System.IO.File.Delete(output_path);
                throw new Exception(
                    $"expected to read ({target_chunk_size}) bytes but instead got ({num_bytes_read})");
            } else {
                is_valid = true;
                Console.WriteLine($"saved file => '{output_path}'");
            }
        } finally {
            ArrayPool<byte>.Shared.Return(read_buffer);
            fs.Close();
            if (!is_valid && System.IO.File.Exists(output_path)) {
                System.IO.File.Delete(output_path);
            }
        }
        return Ok();
    }
}