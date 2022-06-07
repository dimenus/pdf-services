using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PdfServices.Lib;

public class ZMuPdfLib : IDisposable
{
    private record BufferMemory
    {
        public IMemoryOwner<byte> Owner = null!;
        public MemoryHandle Handle;

        public void Destroy()
        {
            Handle.Dispose();
            Owner.Dispose();
        }
    }

    private readonly IntPtr _mRawContext;
    private readonly List<BufferMemory> _sourceBufferList;

    private ZMuPdfLib(IntPtr ctx)
    {
        _mRawContext = ctx;
        _sourceBufferList = new List<BufferMemory>(2048);
    }
    public static ZMuPdfLib Create()
    {
        var ctx = ZMuPdfNativeMethods.CreateContext();
        Debug.Assert(ctx != IntPtr.Zero);
        return new ZMuPdfLib(ctx);
    }

    public void OpenOutput()
    {
        CheckErrorState(ZMuPdfNativeMethods.CreateOutput(_mRawContext));
    }

    public void DropOutput()
    {
        ZMuPdfNativeMethods.DropOutput(_mRawContext);
        foreach (var item in _sourceBufferList) {
            item.Destroy();
        }
        _sourceBufferList.Clear();
    }

    public void AddToOutput(Span<byte> fileBytes)
    {
        if (fileBytes.Length == 0)
            throw new ArgumentException($"{nameof(fileBytes)} must have length > 0");

        var owned_mem = MemoryPool<byte>.Shared.Rent(fileBytes.Length);
        fileBytes.CopyTo(owned_mem.Memory.Span);
        var mem_handle = owned_mem.Memory.Pin();
        try {
            unsafe {
                CheckErrorState(ZMuPdfNativeMethods.AddToOutput(_mRawContext, (byte*) mem_handle.Pointer,
                    (uint) fileBytes.Length));
            }
        } catch {
            mem_handle.Dispose();
        }

        _sourceBufferList.Add(new BufferMemory {
            Handle = mem_handle,
            Owner = owned_mem,
        });
    }

    public void AddPartialToOutput(Span<byte> fileBytes, int firstPageIndex, int length)
    {
        unsafe {
            if (fileBytes.Length == 0) 
                throw new ArgumentException($"{nameof(fileBytes)} must have length > 0");
            
            fixed (byte* ptr = &MemoryMarshal.GetReference(fileBytes)) {
                CheckErrorState(ZMuPdfNativeMethods.AddSelectedToOutput(_mRawContext, ptr, (uint) fileBytes.Length, 
                    firstPageIndex, length));
            }
        }
    }

    public Span<byte> CombineOutput()
    {
        var req_size = ZMuPdfNativeMethods.OutputGetMaxSize(_mRawContext);
        var output_buffer = new byte[req_size].AsSpan();
        output_buffer[0] = 4;

        unsafe {
            fixed (byte* ptr = &MemoryMarshal.GetReference(output_buffer)) {
                CheckErrorState(ZMuPdfNativeMethods.CombineOutputIntoBuffer(_mRawContext, ptr, &req_size, null, 0));
            }
        }
        
        return output_buffer[..(int)req_size];
    }
    
    public void Dispose()
    {
        DropOutput();
        ZMuPdfNativeMethods.DestroyContext(_mRawContext);
    }

    private void CheckErrorState(ZMuPdfNativeMethods.ErrorCode ec)
    {
        if (ec != ZMuPdfNativeMethods.ErrorCode.None)
            throw new Exception($"received '{ec}' ErrorCode");
    }
}