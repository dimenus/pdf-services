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
    
    [TestCase("samples/combineFilesInput1.pdf")]
    [TestCase("samples/combineFilesInput2.pdf")]
    [TestCase("samples/Workflow_FEP5.pdf")]
    [TestCase("samples/WorkView_FEP5.pdf")]
    [TestCase("samples/MRMUnity_FEP5.pdf")]
    public void UploadSingleFile(string filePath)
    {
        var client = new PdfServiceClient(_httpClient);
        client.UploadPdf(filePath);
    }
}