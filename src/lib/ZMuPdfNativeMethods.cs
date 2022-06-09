using System;
using System.Runtime.InteropServices;

namespace PdfServices.Lib;

internal static class ZMuPdfNativeMethods
{
    public enum ErrorCode
    {
        None,
        InternalError,
        InvalidContext,
        InvalidOperation,
        InvalidParameter,
        OperationError,
    }
    
    private const string LIB_NAME = "libzmupdf.so";

    [DllImport(LIB_NAME, EntryPoint ="zmupdf_startup")]
    public static extern IntPtr CreateContext();

    [DllImport(LIB_NAME, EntryPoint = "zmupdf_shutdown")]
    public static extern void DestroyContext(IntPtr context);

    [DllImport(LIB_NAME, EntryPoint = "zmupdf_output_open")]
    public static extern ErrorCode CreateOutput(IntPtr context);

    [DllImport(LIB_NAME, EntryPoint = "zmupdf_output_drop")]
    public static extern void DropOutput(IntPtr context);

    [DllImport(LIB_NAME, EntryPoint = "zmupdf_output_save")]
    public static extern unsafe ErrorCode SaveOutput(IntPtr context, byte* filePath, uint length);

    [DllImport(LIB_NAME, EntryPoint = "zmupdf_input_open_path")]
    public static extern unsafe ErrorCode OpenInputPath(IntPtr context, byte* filePath, uint length,
        uint* outInputHandle);

    [DllImport(LIB_NAME, EntryPoint = "zmupdf_input_drop")]
    public static extern ErrorCode DropInput(IntPtr context, uint inputHandle);

    [DllImport(LIB_NAME, EntryPoint = "zmupdf_input_get_page_count")]
    public static extern unsafe ErrorCode GetInputPageCount(IntPtr context, uint inputHandle, uint* outputPageCount);

    [DllImport(LIB_NAME, EntryPoint = "zmupdf_output_copy_pages")]
    public static extern ErrorCode CopyPagesToOutput(IntPtr ctx, uint inputHandle, int offset, int length);
}