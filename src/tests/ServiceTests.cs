using System.Collections.Generic;
using System.IO;
using System.Net.Http;

namespace PdfServices.Tests;

using System;
using NUnit.Framework;

public class ServiceTests
{
#pragma warning disable CS8618
    private HttpClient _httpClient;
#pragma warning restore CS8618
    
    [SetUp]
    public void Setup()
    {
        var handler = new HttpClientHandler();
        handler.ClientCertificateOptions = ClientCertificateOption.Manual;
        handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
        _httpClient = new HttpClient(handler);
        _httpClient.BaseAddress = new Uri("https://localhost:7212");
        _ = _httpClient.GetAsync("/health").Result;
    }

    [TearDown]
    public void Teardown()
    {
        _httpClient.Dispose();
    }
    
    [TestCase("samples/0_warmup.pdf")]
    [TestCase("samples/1_single_chunk.pdf")]
    [TestCase("samples/2_multi_chunk.pdf")]
    public void UploadSingleFile(string filePath)
    {
        var client = new PdfServiceClient(_httpClient);
        client.UploadPdf(filePath);
    }

    [TestCase("samples/0_warmup.pdf")]
    [TestCase("samples/1_single_chunk.pdf")]
    [TestCase("samples/2_multi_chunk.pdf")]
    [TestCase("samples/3_multi_chunk.pdf")]
    public void SelectFirstPage(string filePath)
    {
        var client = new PdfServiceClient(_httpClient);
        var pdf_obj = client.UploadPdf(filePath).Result;
        var page_bytes = client.GetPdfPage(pdf_obj.Id, 1).Result;
        var file_info = new FileInfo(filePath);
        Directory.CreateDirectory("outputs");
        File.WriteAllBytes(Path.Combine("outputs", file_info.Name), page_bytes);

    }
}