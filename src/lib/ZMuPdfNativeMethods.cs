using System;
using System.Runtime.InteropServices;

namespace PdfServices.Lib;

internal static class ZMuPdfNativeMethods
{
    public enum ErrorCode
    {
        None,
        UnexpectedError,
        InvalidState,
        InvalidParameter,
        BufferTooSmall,
        InvalidFileType
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

    [DllImport(LIB_NAME, EntryPoint = "zmupdf_output_add")]
    public static extern unsafe ErrorCode AddToOutput(IntPtr context, byte* data, nuint size);

    [DllImport(LIB_NAME, EntryPoint = "zmupdf_output_add_selected")]
    public static extern unsafe ErrorCode AddSelectedToOutput(IntPtr context, byte* data, nuint size,
            int minIndex, int rawLength);

    [DllImport(LIB_NAME, EntryPoint = "zmupdf_output_combine")]
    public static extern unsafe ErrorCode CombineOutputIntoBuffer(IntPtr ctx, byte* data, nuint* size, uint* indices, uint numIndices);

    [DllImport(LIB_NAME, EntryPoint = "zmupdf_output_size")]
    public static extern nuint OutputGetMaxSize(IntPtr ctx);
}