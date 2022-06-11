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
        
        [TestCase("samples/combineFilesInput1.pdf", "samples/combineFilesInput2.pdf")]
        [TestCase(
            "samples/combineFilesInput1.pdf",
            "samples/combineFilesInput2.pdf",
            "samples/combineFilesInput1.pdf"
        )]
        public void MuPdfCreateOutput(params string[] inputFiles)
        {
            var stopwatch = Stopwatch.StartNew();
            _mLibContext.OpenOutput();
            foreach (var item in inputFiles) {
                var pdf_handle = _mLibContext.OpenInput(item);
                _mLibContext.CopyPagesToOutput(pdf_handle);
                _mLibContext.DropInput(pdf_handle);
            }
            _mLibContext.SaveOutput("outputs/mupdf_output.pdf");
            _mLibContext.DropOutput();
            
            var memory_diff = Process.GetCurrentProcess().PeakWorkingSet64 - _workingSet;
            Console.WriteLine($"working_set_diff ({memory_diff:N0}), processing time => {stopwatch.Elapsed}");
        }
        
        [Test]
        public void MuPdfCreateLargeOutput()
        {
            var stopwatch = Stopwatch.StartNew();
            _mLibContext.OpenOutput();
            foreach (var item in Directory.GetFiles("samples/", "*.pdf")) {
                var pdf_handle = _mLibContext.OpenInput(item);
                _mLibContext.CopyPagesToOutput(pdf_handle);
                _mLibContext.DropInput(pdf_handle);
            }
            _mLibContext.SaveOutput("outputs/mupdf_large_output.pdf");
            _mLibContext.DropOutput();
            
            var memory_diff = Process.GetCurrentProcess().PeakWorkingSet64 - _workingSet;
            Console.WriteLine($"working_set_diff ({memory_diff:N0}), processing time => {stopwatch.Elapsed}");
        }
        
        [Test]
        public void MuPdfCreatePartialOutput()
        {
            var stopwatch = Stopwatch.StartNew();
            _mLibContext.OpenOutput();
            
            var first_pdf = _mLibContext.OpenInput("samples/combineFilesInput1.pdf");
            _mLibContext.CopyPagesToOutput(first_pdf, 2, 2);
            _mLibContext.DropInput(first_pdf);

            var second_pdf = _mLibContext.OpenInput("samples/combineFilesInput2.pdf");
            _mLibContext.CopyPagesToOutput(first_pdf, 0, 4);
            _mLibContext.DropInput(second_pdf);
            
            _mLibContext.SaveOutput("outputs/mupdf_partial_samples.pdf");
            _mLibContext.DropOutput();
            
            var memory_diff = Process.GetCurrentProcess().PeakWorkingSet64 - _workingSet;
            Console.WriteLine($"working_set_diff ({memory_diff:N0}), processing time => {stopwatch.Elapsed}");
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
            //output_doc.EnableMemoryOptimization = true;
            
            foreach (var input_file in inputs) {
                var file_bytes = File.ReadAllBytes(input_file);
                var loaded_doc = new PdfLoadedDocument(file_bytes);
                PdfDocumentBase.Merge(output_doc, loaded_doc);
                loaded_doc.Close();
            }
            
            var memory_diff = Process.GetCurrentProcess().PeakWorkingSet64 - _workingSet;
            Console.WriteLine($"working_set_diff ({memory_diff:N0}), processing time => {stopwatch.Elapsed}");
            using var output_stream = new FileStream("outputs/sf_output.pdf", FileMode.Create);
            output_doc.Save(output_stream);
        }

        [Test]
        public void SyncFusionCreateLargeOutput()
        {
            var stopwatch = Stopwatch.StartNew();
            using var output_doc = new PdfDocument();

            var file_list = Directory.GetFiles("samples/", "*.pdf");
            var list_file_streams = new List<FileStream>(file_list.Length);
            foreach (var item in file_list) {
                var file_stream = File.Open(item, FileMode.Open);
                var loaded_doc = new PdfLoadedDocument(file_stream);
                PdfDocumentBase.Merge(output_doc, loaded_doc);
                loaded_doc.Close();
                list_file_streams.Add(file_stream);
            }

            var memory_diff = Process.GetCurrentProcess().PeakWorkingSet64 - _workingSet;
            Console.WriteLine($"working_set_diff ({memory_diff:N0}), processing time => {stopwatch.Elapsed}");
            using var output_stream = new FileStream("outputs/sf_large_output.pdf", FileMode.Create);
            output_doc.Save(output_stream);
            output_doc.Close();
            foreach (var item in list_file_streams) {
                item.Close();
            }
        }
    }
}