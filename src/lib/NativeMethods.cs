using System;
using System.Runtime.InteropServices;

namespace PdfServices.Lib;

static class NativeMethods
{
    private const uint FZ_STORE_DEFAULT = 256 << 20;
    private const string FZ_VERSION = "1.19.0";
    private const string LIB_NAME = "libmupdf.so";

    [DllImport(LIB_NAME, EntryPoint = "fz_new_context_imp")]
    private static extern IntPtr NewContext(IntPtr alloc, IntPtr locks, uint maxStore, string version);
    
    public static IntPtr CreateContext()
    {
        return NewContext(IntPtr.Zero, IntPtr.Zero, FZ_STORE_DEFAULT, FZ_VERSION);
    }

    [DllImport(LIB_NAME, EntryPoint = "fz_clone_context")]
    public static extern IntPtr CloneContext(IntPtr fzContext);
    
    [DllImport(LIB_NAME, EntryPoint = "fz_drop_context")]
    public static extern IntPtr DropContext(IntPtr fzContext);

    [DllImport(LIB_NAME, EntryPoint = "fz_new_buffer")]
    public static extern IntPtr CreateBuffer(IntPtr fzContext, nuint size);

    [DllImport(LIB_NAME, EntryPoint = "fz_new_buffer_from_shared_data")]
    public static extern unsafe IntPtr CreateBufferWithData(IntPtr fzContext, byte* data, nuint size);

    [DllImport(LIB_NAME, EntryPoint = "fz_drop_buffer")]
    public static extern void DropBuffer(IntPtr fzContext, IntPtr fzBuffer);

    [DllImport(LIB_NAME, EntryPoint = "fz_new_output")]
    public static extern IntPtr CreateOutput(IntPtr fzContext);

    [DllImport(LIB_NAME, EntryPoint = "fz_new_output_with_buffer")]
    public static extern IntPtr CreateOutputWithBuffer(IntPtr fzContext, IntPtr fzBuffer);

    [DllImport(LIB_NAME, EntryPoint = "fz_close_output")]
    public static extern void CloseOutput(IntPtr fzContext, IntPtr fzOutput);

    [DllImport(LIB_NAME, EntryPoint = "pdf_new_graft_map")]
    public static extern IntPtr CreatePdfGraftMapHandle(IntPtr fzContext, IntPtr dstPdfHandle);

    [DllImport(LIB_NAME, EntryPoint = "pdf_drop_graft_map")]
    public static extern void DropPdfGraftMap(IntPtr fzContext, IntPtr pdfGraftMap);

    [DllImport(LIB_NAME, EntryPoint = "pdf_graft_mapped_page")]
    public static extern void PdfGraftMappedPage(IntPtr fzContext, IntPtr pdfGraftMap, nuint destPageIndex,
        IntPtr srcPdfHandle, uint srcPageIndex);

    [DllImport(LIB_NAME, EntryPoint = "pdf_create_document", CharSet = CharSet.Ansi)]
    public static extern IntPtr CreatePdfHandle(IntPtr fzContext);

    [DllImport(LIB_NAME, EntryPoint = "pdf_open_document_with_stream")]
    public static extern IntPtr CreatePdfHandleFromStream(IntPtr fzContext, IntPtr fzStream);

    [DllImport(LIB_NAME, EntryPoint = "pdf_drop_document")]
    public static extern void DropPdfHandle(IntPtr fzContext, IntPtr pdfHandle);

    [DllImport(LIB_NAME, EntryPoint = "pdf_write_document")]
    public static extern void WriteOutputToPdfHandle(IntPtr fzContext, IntPtr pdfHandle, IntPtr fzOutput, IntPtr opts);

    [DllImport(LIB_NAME, EntryPoint = "fz_open_buffer")]
    public static extern IntPtr CreateStreamFromBuffer(IntPtr fzContext, IntPtr fzBuffer);

    [DllImport(LIB_NAME, EntryPoint = "fz_drop_stream")]
    public static extern IntPtr DropStream(IntPtr fzContext, IntPtr fzStream);
}