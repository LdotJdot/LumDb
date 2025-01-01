using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LumDbEngine.Utils.ByteUtils
{
    public unsafe class FixedStackallocMemoryStream : Stream
    {
        private byte* _buffer;    // Either allocated internally or externally.
        private readonly int _origin;       // For user-provided arrays, start at this origin
        private int _position;     // read/write head.
        private int _length;       // Number of bytes within the memory stream
        // Note that _capacity == _buffer.Length for non-user-provided byte[]'s

        private bool _writable;    // Can user write to this stream?
        private bool _isOpen;      // Is this stream open or closed?
        private Span<byte> span => new Span<byte>(_buffer, _length);
        private Span<byte> currentSpan => new Span<byte>(_buffer, _length).Slice(_position, _length - _position);

        private const int MemStreamMaxLength = int.MaxValue;


        public FixedStackallocMemoryStream(byte* stackAllocBuffer, int length)
        {
            _buffer = stackAllocBuffer;
            _writable = true;
            _isOpen = true;
            _length = length;
        }
        
        public override bool CanRead => _isOpen;

        public override bool CanSeek => _isOpen;

        public override bool CanWrite => _writable;
              
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _isOpen = false;
                _writable = false;
            }
        }

        // returns a bool saying whether we allocated a new array.
        private bool EnsureCapacity(int value)
        {

            if (value > _length)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public override void Flush()
        {
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled(cancellationToken);

            try
            {
                Flush();
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }



        // PERF: Get actual length of bytes available for read; do sanity checks; shift position - i.e. everything except actual copying bytes
        internal int InternalEmulateRead(int count)
        {
            int n = _length - _position;
            if (n > count)
                n = count;
            if (n < 0)
                n = 0;

            Debug.Assert(_position + n >= 0, "_position + n >= 0");  // len is less than 2^31 -1.
            _position += n;
            return n;
        }

        
        public override long Length
        {
            get
            {
                return _length - _origin;
            }
        }

        public override long Position
        {
            get
            {
                return _position - _origin;
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ValidateBufferArguments(buffer, offset, count);

            int n = _length - _position;
            if (n > count)
                n = count;
            if (n <= 0)
                return 0;

            Debug.Assert(_position + n >= 0, "_position + n >= 0");  // len is less than 2^31 -1.

            if (n <= 8)
            {
                int byteCount = n;
                while (--byteCount >= 0)
                    buffer[offset + byteCount] = _buffer[_position + byteCount];
            }
            else
            {
                span.Slice(offset, count).CopyTo(buffer);
            }
            _position += n;

            return n;
        }

        public override int Read(Span<byte> buffer)
        {
            if (GetType() != typeof(FixedStackallocMemoryStream))
            {
                // MemoryStream is not sealed, and a derived type may have overridden Read(byte[], int, int) prior
                // to this Read(Span<byte>) overload being introduced.  In that case, this Read(Span<byte>) overload
                // should use the behavior of Read(byte[],int,int) overload.
                return base.Read(buffer);
            }


            int n = Math.Min(_length - _position, buffer.Length);
            if (n <= 0)
                return 0;
                        
            span.Slice(_position,n).CopyTo(buffer);

            _position += n;
            return n;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();

        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
           throw new NotSupportedException();
        }

        public override int ReadByte()
        {
            if (_position >= _length)
                return -1;

            return _buffer[_position++];
        }

        public override void CopyTo(Stream destination, int bufferSize)
        {
            // If we have been inherited into a subclass, the following implementation could be incorrect
            // since it does not call through to Read() which a subclass might have overridden.
            // To be safe we will only use this implementation in cases where we know it is safe to do so,
            // and delegate to our base class (which will call into Read) when we are not sure.
            if (GetType() != typeof(FixedStackallocMemoryStream))
            {
                base.CopyTo(destination, bufferSize);
                return;
            }

            // Validate the arguments the same way Stream does for back-compat.
            ValidateCopyToArguments(destination, bufferSize);

            int originalPosition = _position;

            // Seek to the end of the MemoryStream.
            int remaining = InternalEmulateRead(_length - originalPosition);

            // If we were already at or past the end, there's no copying to do so just quit.
            if (remaining > 0)
            {
                // Call Write() on the other Stream, using our internal buffer and avoiding any
                // intermediary allocations.
                destination.Write(span);
            }
        }

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin loc)
        {

            if (offset > MemStreamMaxLength)
                throw new ArgumentOutOfRangeException(nameof(offset));

            switch (loc)
            {
                case SeekOrigin.Begin:
                    {
                        int tempPosition = unchecked(_origin + (int)offset);
                        if (offset < 0 || tempPosition < _origin)
                            throw new IOException();
                        _position = tempPosition;
                        break;
                    }
                case SeekOrigin.Current:
                    {
                        int tempPosition = unchecked(_position + (int)offset);
                        if (unchecked(_position + offset) < _origin || tempPosition < _origin)
                            throw new IOException();
                        _position = tempPosition;
                        break;
                    }
                case SeekOrigin.End:
                    {
                        int tempPosition = unchecked(_length + (int)offset);
                        if (unchecked(_length + offset) < _origin || tempPosition < _origin)
                            throw new IOException();
                        _position = tempPosition;
                        break;
                    }
                default:
                    throw new ArgumentException();
            }

            Debug.Assert(_position >= 0, "_position >= 0");
            return _position;
        }

        // Sets the length of the stream to a given value.  The new
        // value must be nonnegative and less than the space remaining in
        // the array, int.MaxValue - origin
        // Origin is 0 in all cases other than a MemoryStream created on
        // top of an existing array and a specific starting offset was passed
        // into the MemoryStream constructor.  The upper bounds prevents any
        // situations where a stream may be created on top of an array then
        // the stream is made longer than the maximum possible length of the
        // array (int.MaxValue).
        //
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }
                
        public override void Write(byte[] buffer, int offset, int count)
        {
            ValidateBufferArguments(buffer, offset, count);
        
            int i = _position + count;
            // Check for overflow
            if (i < 0)
                throw new IOException();

            if (i > _length)
            {
                throw new ArgumentOutOfRangeException();
            }
            else if ((count <= 8))
            {
                int byteCount = count;
                while (--byteCount >= 0)
                {
                    _buffer[_position + byteCount] = buffer[offset + byteCount];
                }
            }
            else
            {
                buffer.AsSpan(offset,count).CopyTo(currentSpan);
            }
            _position = i;
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (GetType() != typeof(FixedStackallocMemoryStream))
            {
                // MemoryStream is not sealed, and a derived type may have overridden Write(byte[], int, int) prior
                // to this Write(Span<byte>) overload being introduced.  In that case, this Write(Span<byte>) overload
                // should use the behavior of Write(byte[],int,int) overload.
                base.Write(buffer);
                return;
            }


            // Check for overflow
            int i = _position + buffer.Length;
            if (i < 0)
                throw new IOException();

            if (i > _length)
            {
                throw new ArgumentOutOfRangeException();              
            }

            buffer.CopyTo(currentSpan);         
            _position = i;
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();

        }

        public override void WriteByte(byte value)
        {
          
            if (_position >= _length)
            {
                throw new ArgumentOutOfRangeException();
            }
            _buffer[_position++] = value;
        }

        // Writes this MemoryStream to another stream.
        public virtual void WriteTo(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);

            stream.Write(span);
        }
    }
}
