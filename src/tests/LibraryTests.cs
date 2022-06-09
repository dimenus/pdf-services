using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using PdfServices.Lib;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Parsing;

namespace PdfServices.Tests
{
    public class LibraryTests
    {
        private long _workingSet;
        private ZMuPdfLib _mLibContext = null!;
        
        [SetUp]
        public void Setup()
        {
            _workingSet = Process.GetCurrentProcess().PeakWorkingSet64;
            _mLibContext = ZMuPdfLib.Create();
            Directory.CreateDirectory("outputs");
        }

        [TearDown]
        public void Shutdown()
        {
            _mLibContext.Dispose();
        }
        
        [TestCase(new [] { "samples/combineFilesInput1.pdf", "samples/combineFilesInput2.pdf"}, "output")]
        public void CreateOutput(string[] inputFiles, string outputPath)
        {
            var stopwatch = Stopwatch.StartNew();
            _mLibContext.OpenOutput();
            foreach (var item in inputFiles) {
                //Get Input Index from Lib
                //Graft 
                AddSampleFile(item);
            }
            var returned_bytes = _mLibContext.CombineOutput();
            var memory_diff = Process.GetCurrentProcess().PeakWorkingSet64 - _workingSet;
            Console.WriteLine($"working_set_diff ({memory_diff:N0}), processing time => {stopwatch.Elapsed}");

            using var sw = File.Open("outputs/samples.pdf", FileMode.Create);
            sw.Write(returned_bytes);
            sw.Flush();
            sw.Close();
            _mLibContext.DropOutput();
        }
        
        [Test]
        public void CreateLargeOutput()
        {
            var stopwatch = Stopwatch.StartNew();
            _mLibContext.OpenOutput();
            foreach (var item in Directory.GetFiles("samples/", "*.pdf")) {
                AddSampleFile(item);
            }
            var returned_bytes = _mLibContext.CombineOutput();
            var memory_diff = Process.GetCurrentProcess().PeakWorkingSet64 - _workingSet;
            Console.WriteLine($"working_set_diff ({memory_diff:N0}), processing time => {stopwatch.Elapsed}");
            
            using var sw = File.Open("outputs/large_sample.pdf", FileMode.Create);
            sw.Write(returned_bytes);
            sw.Flush();
            sw.Close();
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
            var memory_diff = Process.GetCurrentProcess().PeakWorkingSet64 - _workingSet;
            Console.WriteLine($"working_set_diff ({memory_diff:N0}), processing time => {stopwatch.Elapsed}");
            
            using var sw = File.Open("outputs/partial_samples.pdf", FileMode.Create);
            sw.Write(returned_bytes);
            sw.Flush();
            sw.Close();
            _mLibContext.DropOutput();
        }

        [TestCase("samples/combineFilesInput1.pdf", "samples/combineFilesInput2.pdf")]
        [TestCase(
            "samples/combineFilesInput1.pdf",
            "samples/combineFilesInput2.pdf",
            "samples/combineFilesInput1.pdf"
        )]
        public void SyncFusionCreateOutput(params string[] inputs)
        {
            var stopwatch = Stopwatch.StartNew();
            using var output_doc = new PdfDocument();
            
            foreach (var input_file in inputs) {
                var file_bytes = File.ReadAllBytes(input_file);
                var loaded_doc = new PdfLoadedDocument(file_bytes);
                PdfDocumentBase.Merge(output_doc, loaded_doc);
                loaded_doc.Close();
            }
            
            var memory_diff = Process.GetCurrentProcess().PeakWorkingSet64 - _workingSet;
            Console.WriteLine($"working_set_diff ({memory_diff:N0}), processing time => {stopwatch.Elapsed}");
            using var output_stream = new FileStream("outputs/sync_fusion_sample.pdf", FileMode.Create);
            output_doc.Save(output_stream);
            output_stream.Flush();
        }

        [Test]
        public void SyncFusionCreateLargeOutput()
        {
            var stopwatch = Stopwatch.StartNew();
            using var output_doc = new PdfDocument();

            var file_list = Directory.GetFiles("samples/", "*.pdf");
            foreach (var item in file_list) {
                var file_bytes = File.ReadAllBytes(item);
                var loaded_doc = new PdfLoadedDocument(file_bytes);
                PdfDocumentBase.Merge(output_doc, loaded_doc);
                loaded_doc.Close();
            }

            var memory_diff = Process.GetCurrentProcess().PeakWorkingSet64 - _workingSet;
            Console.WriteLine($"working_set_diff ({memory_diff:N0}), processing time => {stopwatch.Elapsed}");
            using var output_stream = new FileStream("outputs/sync_fusion_sample.pdf", FileMode.Create);
            output_doc.Save(output_stream);
            output_stream.Flush();
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