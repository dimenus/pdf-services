using System;
using System.Buffers;

namespace PdfServices.Service.Utils;

public class ScopedCharBuffer : IDisposable
{
    private int _position = 0;
    private readonly ArrayPool<char> _arrayPool;
    private char[]? _buffer;
    
    public ScopedCharBuffer(int capacity, ArrayPool<char>? arrayPool = null)
    {
        _arrayPool = arrayPool ?? ArrayPool<char>.Shared;
        _buffer = _arrayPool.Rent(capacity);
    }

    private void ValidateStorage(int spanLength)
    {
        if (_buffer is null) throw new InvalidOperationException($"Buffer is not initialized");
        var require_space = spanLength + _position;
        if (_buffer.Length < spanLength + _position)
            throw new OutOfMemoryException(
                $"Buffer operation requires ({{require_space}}) but is of length ({_buffer.Length})");
    }

    public void Add(Span<char> value)
    {
        ValidateStorage(value.Length);
        value.CopyTo(_buffer.AsSpan()[_position..]);
        _position += value.Length;
    }
    
    public void Add(string value)
    {
        var value_span = value.AsSpan();
        ValidateStorage(value_span.Length);
        value_span.CopyTo(_buffer.AsSpan()[_position..]);
        _position += value_span.Length;
    }
    
    public void Dispose()
    {
        if (_buffer is null) return;
        _arrayPool.Return(_buffer);
        _buffer = null;
    }

    public void Reset()
    {
        ValidateStorage(0);
        _position = 0;
    }

    public char[] GetValue()
    {
        ValidateStorage(0);
        return _buffer!;
    }
}