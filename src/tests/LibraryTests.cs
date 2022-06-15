using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using PdfServices.Lib;

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
        
        [TestCase("samples/1_single_chunk.pdf", "samples/1_single_chunk.pdf")]
        [TestCase("samples/1_single_chunk.pdf", "samples/2_multi_chunk.pdf")]
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
        
        [TestCase("samples/", "*.pdf")]
        public void MuPdfCreateLargeOutput(string dirName, string searchPrefix)
        {
            var stopwatch = Stopwatch.StartNew();
            _mLibContext.OpenOutput();
            foreach (var item in Directory.GetFiles(dirName, searchPrefix)) {
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
            
            var first_pdf = _mLibContext.OpenInput("samples/1_single_chunk.pdf");
            _mLibContext.CopyPagesToOutput(first_pdf, 2, 2);
            _mLibContext.DropInput(first_pdf);

            var second_pdf = _mLibContext.OpenInput("samples/2_multi_chunk.pdf");
            _mLibContext.CopyPagesToOutput(first_pdf, 0, 4);
            _mLibContext.DropInput(second_pdf);
            
            _mLibContext.SaveOutput("outputs/mupdf_partial_samples.pdf");
            _mLibContext.DropOutput();
            
            var memory_diff = Process.GetCurrentProcess().PeakWorkingSet64 - _workingSet;
            Console.WriteLine($"working_set_diff ({memory_diff:N0}), processing time => {stopwatch.Elapsed}");
        }
    }
}