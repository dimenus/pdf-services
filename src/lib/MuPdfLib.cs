using System;
using System.Buffers;
using System.Collections.Generic;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using System.Threading;

namespace PdfServices.Lib;

public static class MuPdfLib
{
    private static long s_createLock;
    private static bool s_contextExists;

    public static MuPdfContext CreateContext()
    {
        while (Interlocked.CompareExchange(ref s_createLock, 1, 0) != 1) {
            Thread.Sleep(5);
        }
        if (s_contextExists)
            throw new NotImplementedException($"calling {nameof(CreateContext)} multiple times is not supported yet.");
        var new_context = Helpers.ValidateNonNull(MuPdfNativeMethods.CreateContext());
        s_contextExists = true;
        s_createLock = 0;
        return new MuPdfContext(new_context);
    }
}

public class MuPdfContext : IDisposable
{
    enum ContextState
    {
        Open,
        ExistingBatch,
        FinalizedBatch
    }

    private struct MappedPdf
    {
        public MappedPdf(IntPtr bufferHandle, IntPtr streamHandle, IntPtr pdfHandle)
        {
            BufferHandle = bufferHandle;
            StreamHandle = streamHandle;
            PdfHandle = pdfHandle;
        }

        public readonly IntPtr BufferHandle;
        public readonly IntPtr StreamHandle;
        public readonly IntPtr PdfHandle;
    }

    private readonly MemoryPool<byte> _memoryPool = MemoryPool<byte>.Shared;
    private readonly IntPtr _fzContext;
    private ContextState _state;
    private List<MappedPdf> _mappedPdfList;
    private int _totalBatchSize = 0;
    private IMemoryOwner<byte>? _output = null;

    const int DEFAULT_BUFFER_CAPACITY = 256;

    internal MuPdfContext(IntPtr fzContext)
    {
        Console.WriteLine(MemoryPool<byte>.Shared.MaxBufferSize);
        _fzContext = fzContext;
        _state = ContextState.Open;
        _mappedPdfList = new List<MappedPdf>(DEFAULT_BUFFER_CAPACITY);
        
    }

    public void Dispose()
    {
        Reset();
        MuPdfNativeMethods.DropContext(_fzContext);
    }

    public void OpenBatch(int capacity = DEFAULT_BUFFER_CAPACITY)
    {
        if (capacity > _mappedPdfList.Capacity) {
            _mappedPdfList = new List<MappedPdf>(capacity);
        }
        _state = ContextState.ExistingBatch;
    }

    public unsafe void AddToBatch(ReadOnlySpan<byte> fileBytes)
    {
        if (fileBytes.Length == 0) 
            throw new ArgumentException($"{nameof(fileBytes)} must have length > 0");
        
        fixed (byte* ptr = &MemoryMarshal.GetReference<byte>(fileBytes)) {
            var buf_handle = Helpers.ValidateNonNull(
                MuPdfNativeMethods.CreateBufferWithData(_fzContext, ptr, (nuint) fileBytes.Length));

            var stream_handle = Helpers.ValidateNonNull(
                MuPdfNativeMethods.CreateStreamFromBuffer(_fzContext, buf_handle));

            var pdf_handle = Helpers.ValidateNonNull(
                MuPdfNativeMethods.CreatePdfHandleFromStream(_fzContext, stream_handle));
            
            _mappedPdfList.Add(new MappedPdf(buf_handle, stream_handle, pdf_handle));
            _totalBatchSize += fileBytes.Length;
        }
    }

    public void CombineBatch(string outputPath)
    {
        if (_state != ContextState.ExistingBatch)
            throw new InvalidOperationException($"can't finalize batch in '{_state}' state");

        var fz_output = Helpers.ValidateNonNull(
            MuPdfNativeMethods.CreatePdfHandle(_fzContext));

        var num_pages = 0;

        _state = ContextState.FinalizedBatch;
        
        MuPdfNativeMethods.DropPdfHandle(_fzContext, fz_output);
    }

    public void Reset()
    {
        foreach (var item in _mappedPdfList) {
            MuPdfNativeMethods.DropPdfHandle(_fzContext, item.PdfHandle);
            MuPdfNativeMethods.DropStream(_fzContext, item.StreamHandle);
            MuPdfNativeMethods.DropBuffer(_fzContext, item.BufferHandle);
        }
        
        _mappedPdfList.Clear();
        _state = ContextState.Open;
    }
}

internal static class Helpers
{
    public static IntPtr ValidateNonNull(IntPtr inPtr)
    {
        if (inPtr == IntPtr.Zero) throw new Exception("MuPdf pointer is null");
        return inPtr;
    }
}