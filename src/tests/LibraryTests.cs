using System;
using System.IO;
using NUnit.Framework;
using PdfServices.Lib;

namespace PdfServices.Tests
{
    public class LibraryTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void CreateBatch()
        {
            try {
                using var ctx = MuPdfLib.CreateContext();
                ctx.OpenBatch();

                foreach (var item in Directory.EnumerateFiles("samples")) {
                    var file_bytes = File.ReadAllBytes(item).AsSpan();
                    ctx.AddToBatch(file_bytes);
                }

                ctx.CombineBatch("./test.pdf");

                ctx.Reset();
            } catch {
                
            }
        }
    }
}