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

    public ZMuPdfLib()
    {
        _mRawContext = ZMuPdfNativeMethods.CreateContext();
    }
    public static ZMuPdfLib Create()
    {
        var ctx = ZMuPdfNativeMethods.CreateContext();
        return new ZMuPdfLib(ctx);
    }

    public void OpenOutput()
    {
        CheckErrorState(ZMuPdfNativeMethods.CreateOutput(_mRawContext), nameof(OpenOutput));
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
                CheckErrorState(ZMuPdfNativeMethods.OpenInputPath(_mRawContext, ptr, (uint)file_path.Length, &input_handle), nameof(OpenInput));
            }
            return input_handle;
        }
    }

    public void DropInput(uint inputHandle)
    {
        CheckErrorState(ZMuPdfNativeMethods.DropInput(_mRawContext, inputHandle), nameof(DropInput));
    }

    public void CopyPagesToOutput(uint inputHandle, int offset, int length)
    {
        CheckErrorState(ZMuPdfNativeMethods.CopyPagesToOutput(_mRawContext, inputHandle, offset, length), nameof(CopyPagesToOutput));
    }

    public void CopyPagesToOutput(uint inputHandle)
    {
        uint num_pages = 0;
        unsafe {
            CheckErrorState(ZMuPdfNativeMethods.GetInputPageCount(_mRawContext, inputHandle, &num_pages), nameof(CopyPagesToOutput));
        }
        CopyPagesToOutput(inputHandle, 0, (int)num_pages);
    }

    public void SaveOutput(string filePath)
    {
        if (File.Exists(filePath)) File.Delete(filePath);
        var file_path = Encoding.UTF8.GetBytes(filePath).AsSpan();
        unsafe {
            fixed (byte* ptr = file_path) {
                CheckErrorState(ZMuPdfNativeMethods.SaveOutput(_mRawContext, ptr, (uint)file_path.Length), nameof(SaveOutput));
            }
        }
    }

    public int GetInputPageCount(uint inputHandle)
    {
        uint num_pages = 0;
        unsafe {
            CheckErrorState(ZMuPdfNativeMethods.GetInputPageCount(_mRawContext, inputHandle, &num_pages), nameof(GetInputPageCount));
        }

        return (int)num_pages;
    }

    public void Dispose()
    {
        DropOutput();
        ZMuPdfNativeMethods.DestroyContext(_mRawContext);
    }

    private static void CheckErrorState(ZMuPdfNativeMethods.ErrorCode ec, string methodName)
    {
        if (ec != ZMuPdfNativeMethods.ErrorCode.None)
            throw new ZMuPdfLibException(ec, methodName);
    }
}

public class ZMuPdfLibException : Exception
{
    public enum ErrorCodeType
    {
        User,
        ApiUsage,
        Fatal,
    }

    public readonly ErrorCodeType CodeType;
    public override string Message { get; }

    internal ZMuPdfLibException(ZMuPdfNativeMethods.ErrorCode ec, string methodName) 
        : base($"received ErrorCode '{ec.ToString()}' from internal library")
    {
        switch (ec) {
            case ZMuPdfNativeMethods.ErrorCode.InternalError:
                CodeType = ErrorCodeType.Fatal;
                Message = "An internal error has occurred within the library";
                break;
            case ZMuPdfNativeMethods.ErrorCode.InvalidContext:
                CodeType = ErrorCodeType.ApiUsage;
                Message = "A null IntPtr was passed to a function within the library.";
                break;
            case ZMuPdfNativeMethods.ErrorCode.InvalidOperation:
                Message = "The library usage is incorrect. Consult the test suite.";
                break;
            case ZMuPdfNativeMethods.ErrorCode.InvalidParameter:
                Message = $"An invalid or out of range parameter was passed to method '{methodName}'";
                break;
            case ZMuPdfNativeMethods.ErrorCode.None:
            case ZMuPdfNativeMethods.ErrorCode.OperationError:
            default:
                throw new ArgumentOutOfRangeException(nameof(ec), ec, null);
        }
    }
}