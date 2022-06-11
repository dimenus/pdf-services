using System.Net.Http;

namespace PdfServices.Tests;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using PdfServices.Lib;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Parsing;

public class ServiceTests
{
    private static HttpClient _httpClient;
    
    [SetUp]
    public void Setup()
    {
        var handler = new HttpClientHandler();
        handler.ClientCertificateOptions = ClientCertificateOption.Manual;
        handler.ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, cetChain, policyErrors) => true;
        _httpClient = new HttpClient(handler);
        _httpClient.BaseAddress = new Uri("https://localhost:7212");
        _ = _httpClient.GetAsync("/health").Result;
    }

    [TearDown]
    public void Teardown()
    {
        _httpClient.Dispose();
    }
    
    [Test]
    [TestCase("samples/combineFilesInput2.pdf")]
    [TestCase("samples/Workflow_FEP5.pdf")]
    [TestCase("samples/Workview_FEP5.pdf")]
    [TestCase("samples/MRMUnity_FEP5.pdf")]
    public void UploadSingleFile(string filePath)
    {
        var client = new PdfServiceClient(_httpClient);
        client.UploadPdf(filePath);
    }
}