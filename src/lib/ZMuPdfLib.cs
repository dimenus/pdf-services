using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace PdfServices.Lib;

public class ZMuPdfLib : IDisposable
{
    private readonly IntPtr _mRawContext;

    private ZMuPdfLib(IntPtr ctx)
    {
        _mRawContext = ctx;
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
    }

    public uint OpenInput(string filePath)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException(message: null, fileName: filePath);
        var file_path = Encoding.UTF8.GetBytes(filePath).AsSpan();
        unsafe {
            uint input_handle = 0;
            fixed (byte* ptr = file_path) {
                CheckErrorState(ZMuPdfNativeMethods.OpenInputPath(_mRawContext, ptr, (uint)file_path.Length, &input_handle));
            }
            return input_handle;
        }
    }

    public void DropInput(uint inputHandle)
    {
        CheckErrorState(ZMuPdfNativeMethods.DropInput(_mRawContext, inputHandle));
    }

    public void CopyPagesToOutput(uint inputHandle, int offset, int length)
    {
        CheckErrorState(ZMuPdfNativeMethods.CopyPagesToOutput(_mRawContext, inputHandle, offset, length));
    }

    public void CopyPagesToOutput(uint inputHandle)
    {
        uint num_pages = 0;
        unsafe {
            CheckErrorState(ZMuPdfNativeMethods.GetInputPageCount(_mRawContext, inputHandle, &num_pages));
        }
        CopyPagesToOutput(inputHandle, 0, (int)num_pages);
    }

    public void SaveOutput(string filePath)
    {
        if (File.Exists(filePath)) File.Delete(filePath);
        var file_path = Encoding.UTF8.GetBytes(filePath).AsSpan();
        unsafe {
            fixed (byte* ptr = file_path) {
                CheckErrorState(ZMuPdfNativeMethods.SaveOutput(_mRawContext, ptr, (uint)file_path.Length));
            }
        }
    }

    public void Dispose()
    {
        DropOutput();
        ZMuPdfNativeMethods.DestroyContext(_mRawContext);
    }

    private static void CheckErrorState(ZMuPdfNativeMethods.ErrorCode ec)
    {
        if (ec != ZMuPdfNativeMethods.ErrorCode.None)
            throw new Exception($"received '{ec}' ErrorCode");
    }
}