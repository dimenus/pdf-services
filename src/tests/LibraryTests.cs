using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using PdfServices.Lib;

namespace PdfServices.Tests
{
    public class LibraryTests
    {
        private ZMuPdfLib _mLibContext = null!;
        
        [SetUp]
        public void Setup()
        {
            _mLibContext = ZMuPdfLib.Create();
            Directory.CreateDirectory("outputs");
        }

        [TearDown]
        public void Shutdown()
        {
            _mLibContext.Dispose();
        }

        [TestCase(new [] { "foo", "bar"}, "output")]
        public void CreateOutput(string[] inputFiles, string outputPath)
        {
            var stopwatch = Stopwatch.StartNew();
            _mLibContext.OpenOutput();
            AddSampleFile("samples/combineFilesInput1.pdf");
            AddSampleFile("samples/combineFilesInput2.pdf");
            var returned_bytes = _mLibContext.CombineOutput();
            using var sw = File.Open("outputs/samples.pdf", FileMode.Create);
            sw.Write(returned_bytes);
            sw.Flush();
            sw.Close();
            Console.WriteLine($"processing time => {stopwatch.Elapsed}");
            _mLibContext.DropOutput();
        }
        
        [Test]
        public void CreateLargeOutput()
        {
            var stopwatch = Stopwatch.StartNew();
            _mLibContext.OpenOutput();
            AddSampleFile("samples/Workflow_FEP5.pdf");
            AddSampleFile("samples/Workview_FEP5.pdf");
            var returned_bytes = _mLibContext.CombineOutput();
            using var sw = File.Open("outputs/large_sample.pdf", FileMode.Create);
            sw.Write(returned_bytes);
            sw.Flush();
            sw.Close();
            Console.WriteLine($"processing time => {stopwatch.Elapsed}");
            _mLibContext.DropOutput();
        }
        
        [Test]
        public void CreatePartialOutput()
        {
            var stopwatch = Stopwatch.StartNew();
            _mLibContext.OpenOutput();
            AddPartialSampleFile("samples/combineFilesInput1.pdf", 2, 2);
            AddSampleFile("samples/combineFilesInput2.pdf");
            var returned_bytes = _mLibContext.CombineOutput();
            using var sw = File.Open("outputs/partial_samples.pdf", FileMode.Create);
            sw.Write(returned_bytes);
            sw.Flush();
            sw.Close();
            Console.WriteLine($"processing time => {stopwatch.Elapsed}");
            _mLibContext.DropOutput();
        }

        private void AddSampleFile(string filePath)
        {
            var file_bytes = File.ReadAllBytes(filePath);
            _mLibContext.AddToOutput(file_bytes);
        }

        private void AddPartialSampleFile(string filePath, int pageIndex, int length)
        {
            var file_bytes = File.ReadAllBytes(filePath);
            _mLibContext.AddPartialToOutput(file_bytes, pageIndex, length);
        }
    }
}