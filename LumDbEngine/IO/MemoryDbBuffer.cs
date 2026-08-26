using LumDbEngine.Element.Structure;
using LumDbEngine.Element.Structure.Page;

namespace LumDbEngine.IO
{
    /// <summary>
    /// Process-lifetime committed database image (same layout as a .db file). Internal only.
    /// </summary>
    internal sealed class MemoryDbBuffer
    {
        private byte[] _data;
        private long _length;
        private readonly object _sync = new();

        public MemoryDbBuffer()
        {
            _data = new byte[DbHeader.HEADER_SIZE + BasePage.PAGE_SIZE];
            _length = 0;
        }

        public long Length
        {
            get { lock (_sync) return _length; }
        }

        public void SetLength(long value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            lock (_sync)
            {
                EnsureCapacity(value);
                _length = value;
            }
        }

        public int Read(long position, byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            lock (_sync)
            {
                if (position >= _length || position < 0)
                    return 0;
                int n = (int)Math.Min(count, _length - position);
                Buffer.BlockCopy(_data, (int)position, buffer, offset, n);
                return n;
            }
        }

        public void Write(long position, byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (count == 0)
                return;
            lock (_sync)
            {
                long end = position + count;
                EnsureCapacity(end);
                if (end > _length)
                    _length = end;
                Buffer.BlockCopy(buffer, offset, _data, (int)position, count);
            }
        }

        public byte[] ToArray()
        {
            lock (_sync)
            {
                var copy = new byte[_length];
                if (_length > 0)
                    Buffer.BlockCopy(_data, 0, copy, 0, (int)_length);
                return copy;
            }
        }

        private void EnsureCapacity(long needed)
        {
            if (needed <= _data.Length)
                return;
            long cap = _data.Length;
            while (cap < needed)
                cap = cap < 1_048_576 ? cap * 2 : cap + 1_048_576;
            if (cap > int.MaxValue)
                throw new IOException("In-memory database exceeded 2 GB.");
            var next = new byte[(int)cap];
            if (_length > 0)
                Buffer.BlockCopy(_data, 0, next, 0, (int)_length);
            _data = next;
        }
    }

    /// <summary>
    /// Independent-position Stream over <see cref="MemoryDbBuffer"/> (file-pointer analogue).
    /// </summary>
    internal sealed class MemoryCursorStream : Stream
    {
        private readonly MemoryDbBuffer _buffer;
        private readonly bool _writable;
        private long _position;
        private bool _disposed;

        public MemoryCursorStream(MemoryDbBuffer buffer, bool writable)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _writable = writable;
        }

        public override bool CanRead => !_disposed;
        public override bool CanSeek => !_disposed;
        public override bool CanWrite => _writable && !_disposed;
        public override long Length => _buffer.Length;

        public override long Position
        {
            get => _position;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value));
                _position = value;
            }
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            int n = _buffer.Read(_position, buffer, offset, count);
            _position += n;
            return n;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            long next = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => _buffer.Length + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin))
            };
            if (next < 0)
                throw new IOException("Seek before start of stream.");
            _position = next;
            return _position;
        }

        public override void SetLength(long value)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_writable)
                throw new NotSupportedException();
            _buffer.SetLength(value);
            if (_position > value)
                _position = value;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_writable)
                throw new NotSupportedException();
            _buffer.Write(_position, buffer, offset, count);
            _position += count;
        }

        protected override void Dispose(bool disposing)
        {
            _disposed = true;
            base.Dispose(disposing);
        }
    }
}
