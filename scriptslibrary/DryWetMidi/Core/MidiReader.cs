using Melanchall.DryWetMidi.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Melanchall.DryWetMidi.Core
{
    /// <summary>
    /// Reader of the MIDI data types.
    /// </summary>
    public sealed class MidiReader
    {
        #region Constants

        static readonly byte[] EmptyByteArray = new byte[0];

        #endregion

        #region Fields

        readonly ReaderSettings _settings;

        readonly Stream _stream;
        readonly bool _isStreamWrapped;

        readonly bool _useBuffering;
        byte[] _buffer;
        int _bufferSize;
        int _bufferPosition;
        long _bufferStart = -1;

        long _position;

        bool _disposed;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="MidiReader"/> with the specified stream.
        /// </summary>
        /// <param name="stream">Stream to read MIDI file from.</param>
        /// <param name="settings">Settings according to which MIDI data should be read.</param>
        /// <exception cref="ArgumentNullException">
        /// <para>One of the following errors occured:</para>
        /// <list type="bullet">
        /// <item>
        /// <description><paramref name="stream"/> is <c>null</c>.</description>
        /// </item>
        /// <item>
        /// <description><paramref name="settings"/> is <c>null</c>.</description>
        /// </item>
        /// </list>
        /// </exception>
        /// <exception cref="InvalidOperationException"><see cref="ReaderSettings.Buffer"/> of <paramref name="settings"/>
        /// is <c>null</c> in case of <see cref="ReaderSettings.BufferingPolicy"/> set to
        /// <see cref="BufferingPolicy.UseCustomBuffer"/>.</exception>
        public MidiReader(Stream stream, ReaderSettings settings)
        {
            ThrowIfArgument.IsNull(nameof(stream), stream);
            ThrowIfArgument.IsNull(nameof(settings), settings);

            _settings = settings;

            if (!stream.CanSeek)
            {
                stream = new StreamWrapper(stream, settings.NonSeekableStreamBufferSize);
                _isStreamWrapped = true;
            }

            _stream = stream;
            Length = _stream.Length;

            _useBuffering = _settings.BufferingPolicy != BufferingPolicy.DontUseBuffering && !_isStreamWrapped && !(_stream is MemoryStream);
            if (_useBuffering) PrepareBuffer();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the position within the underlying stream.
        /// </summary>
        /// <exception cref="IOException">An I/O error occurred on the underlying stream.</exception>
        /// <exception cref="ObjectDisposedException">Property was called after the reader was disposed.</exception>
        public long Position
        {
            get => _useBuffering ? _position : _stream.Position;
            set
            {
                if (_useBuffering) _bufferPosition += (int)(value - _position);
                else _stream.Position = value;

                _position = value;
            }
        }

        /// <summary>
        /// Gets length of the underlying stream.
        /// </summary>
        public long Length { get; }

        /// <summary>
        /// Gets a value indicating whether end of the underlying stream is reached.
        /// </summary>
        /// <exception cref="IOException">An I/O error occurred on the underlying stream.</exception>
        /// <exception cref="ObjectDisposedException">Property was called after the reader was disposed.</exception>
        public bool EndReached => Position >= Length || (_isStreamWrapped && ((StreamWrapper)_stream).IsEndReached());

        #endregion

        #region Methods

        /// <summary>
        /// Reads a byte from the underlying stream and advances the current position by one byte.
        /// </summary>
        /// <returns>The next byte read from the underlying stream.</returns>
        /// <exception cref="EndOfStreamException">The end of the underlying stream is reached.</exception>
        /// <exception cref="ObjectDisposedException">Method was called after the reader was disposed.</exception>
        /// <exception cref="IOException">An I/O error occurred on the underlying stream.</exception>
        public byte ReadByte()
        {
            if (_useBuffering)
            {
                if (!EnsureBufferIsReadyForReading()) throw new EndOfStreamException();

                var result = _buffer[_bufferPosition];
                Position++;
                return result;
            }
            else
            {
                var result = _stream.ReadByte();
                if (result < 0) throw new EndOfStreamException();

                return (byte)result;
            }
        }

        /// <summary>
        /// Reads a signed byte from the underlying stream and advances the current position by one byte.
        /// </summary>
        /// <returns>A signed byte read from the underlying stream.</returns>
        /// <exception cref="EndOfStreamException">The end of the underlying stream is reached.</exception>
        /// <exception cref="ObjectDisposedException">Method was called after the reader was disposed.</exception>
        /// <exception cref="IOException">An I/O error occurred on the underlying stream.</exception>
        public sbyte ReadSByte() => (sbyte)ReadByte();

        /// <summary>
        /// Reads the specified number of bytes from the underlying stream into a byte array
        /// and advances the current position by that number of bytes.
        /// </summary>
        /// <param name="count">The number of bytes to read. This value must be 0 or a
        /// non-negative number or an exception will occur.</param>
        /// <returns>A byte array containing data read from the underlying stream. This might be less
        /// than the number of bytes requested if the end of the stream is reached.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
        /// <exception cref="ObjectDisposedException">Method was called after the reader was disposed.</exception>
        /// <exception cref="IOException">An I/O error occurred on the underlying stream.</exception>
        public byte[] ReadBytes(int count)
        {
            if (_isStreamWrapped && count > _settings.NonSeekableStreamIncrementalBytesReadingThreshold)
            {
                var bytesList = new List<byte[]>();

                while (count > 0)
                {
                    var bytes = ReadBytesInternal(Math.Min(count, _settings.NonSeekableStreamIncrementalBytesReadingStep));
                    if (bytes.Length == 0)
                        break;

                    count -= bytes.Length;
                    bytesList.Add(bytes);
                }

                return bytesList.SelectMany(bytes => bytes).ToArray();
            }

            return ReadBytesInternal(count);
        }

        /// <summary>
        /// Reads a WORD value (16-bit unsigned integer) from the underlying stream and
        /// advances the current position by two bytes.
        /// </summary>
        /// <returns>A 16-bit unsigned integer read from the underlying stream.</returns>
        /// <exception cref="ObjectDisposedException">Method was called after the reader was disposed.</exception>
        /// <exception cref="IOException">An I/O error occurred on the underlying stream.</exception>
        /// <exception cref="NotEnoughBytesException">Not enough bytes in the stream to read a WORD.</exception>
        public ushort ReadWord()
        {
            const int wordSize = sizeof(ushort);
            unsafe
            {
                var ptr = stackalloc byte[wordSize];
                long bytesRead = 0;
                long bytesToRead = wordSize;

                while (bytesToRead > 0)
                {
                    if (!EnsureBufferIsReadyForReading())
                        throw new NotEnoughBytesException("Not enough bytes in the stream to read a WORD.", wordSize, bytesRead);

                    long count = Math.Min(bytesToRead, _bufferStart + _bufferSize - _position);

                    fixed (byte* bufferPtr = &_buffer[_bufferPosition])
                        Buffer.MemoryCopy(bufferPtr, ptr + bytesRead, count, count);

                    bytesRead += count;
                    bytesToRead -= count;
                }

                Position += wordSize;
                return (ushort)((ptr[0] << 8) + ptr[1]);
            }
        }

        /// <summary>
        /// Reads a DWORD value (32-bit unsigned integer) from the underlying stream and
        /// advances the current position by four bytes.
        /// </summary>
        /// <returns>A 32-bit unsigned integer read from the underlying stream.</returns>
        /// <exception cref="ObjectDisposedException">Method was called after the reader was disposed.</exception>
        /// <exception cref="IOException">An I/O error occurred on the underlying stream.</exception>
        /// <exception cref="NotEnoughBytesException">Not enough bytes in the stream to read a DWORD.</exception>
        public uint ReadDword()
        {
            const int dwordSize = sizeof(uint);

            var bytes = ReadBytes(dwordSize);
            if (bytes.Length < dwordSize)
                throw new NotEnoughBytesException("Not enough bytes in the stream to read a DWORD.", dwordSize, bytes.Length);

            unsafe
            {
                fixed (byte* pBytes = bytes) 
                    return (uint)(pBytes[0] << 24 | pBytes[1] << 16 | pBytes[2] << 8 | pBytes[3]);
            }
        }

        /// <summary>
        /// Reads an INT16 value (16-bit signed integer) from the underlying stream and
        /// advances the current position by two bytes.
        /// </summary>
        /// <returns>A 16-bit signed integer read from the underlying stream.</returns>
        /// <exception cref="ObjectDisposedException">Method was called after the reader was disposed.</exception>
        /// <exception cref="IOException">An I/O error occurred on the underlying stream.</exception>
        /// <exception cref="NotEnoughBytesException">Not enough bytes in the stream to read a INT16.</exception>
        public short ReadInt16()
        {
            const int int16Size = sizeof(short);

            var bytes = ReadBytes(int16Size);
            if (bytes.Length < int16Size)
                throw new NotEnoughBytesException("Not enough bytes in the stream to read a INT16.", int16Size, bytes.Length);

            unsafe
            {
                fixed (byte* bytePtr = bytes) return (short)((bytePtr[0] << 8) + bytePtr[1]);
            }
        }

        /// <summary>
        /// Reads the specified number of characters from the underlying stream, returns the
        /// data as string, and advances the current position by that number of characters.
        /// </summary>
        /// <param name="count">The length of string to read.</param>
        /// <returns>The string being read.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
        /// <exception cref="ObjectDisposedException">Method was called after the reader was disposed.</exception>
        /// <exception cref="IOException">An I/O error occurred on the underlying stream.</exception>
        public string ReadString(int count)
        {
            var bytes = ReadBytesInternal(count);
            return SmfConstants.DefaultTextEncoding.GetString(bytes);
        }

        /// <summary>
        /// Reads a 32-bit signed integer presented in compressed format called variable-length quantity (VLQ)
        /// to the underlying stream.
        /// </summary>
        /// <remarks>
        /// Numbers in VLQ format are represented 7 bits per byte, most significant bits first.
        /// All bytes except the last have bit 7 set, and the last byte has bit 7 clear. If the
        /// number is between 0 and 127, it is thus represented exactly as one byte.
        /// </remarks>
        /// <returns>A 32-bit signed integer read from the underlying stream.</returns>
        /// <exception cref="NotEnoughBytesException">Not enough bytes in the stream to read a variable-length quantity
        /// number.</exception>
        /// <exception cref="ObjectDisposedException">Method was called after the reader was disposed.</exception>
        /// <exception cref="IOException">An I/O error occurred on the underlying stream.</exception>
        public int ReadVlqNumber() => (int)ReadVlqLongNumber();

        /// <summary>
        /// Reads a 64-bit signed integer presented in compressed format called variable-length quantity (VLQ)
        /// to the underlying stream.
        /// </summary>
        /// <remarks>
        /// Numbers in VLQ format are represented 7 bits per byte, most significant bits first.
        /// All bytes except the last have bit 7 set, and the last byte has bit 7 clear. If the
        /// number is between 0 and 127, it is thus represented exactly as one byte.
        /// </remarks>
        /// <returns>A 64-bit signed integer read from the underlying stream.</returns>
        /// <exception cref="NotEnoughBytesException">Not enough bytes in the stream to read a variable-length quantity
        /// number.</exception>
        /// <exception cref="ObjectDisposedException">Method was called after the reader was disposed.</exception>
        /// <exception cref="IOException">An I/O error occurred on the underlying stream.</exception>
        public long ReadVlqLongNumber()
        {
            long result = 0;
            byte b;

            try
            {
                do
                {
                    b = ReadByte();
                    result = (result << 7) + (b & 127);
                }
                while (b >> 7 != 0);
            }
            catch (EndOfStreamException ex)
            {
                throw new NotEnoughBytesException("Not enough bytes in the stream to read a variable-length quantity number.", ex);
            }

            return result;
        }

        /// <summary>
        /// Reads a DWORD value (32-bit unsigned integer) presented by 3 bytes from the underlying
        /// stream and advances the current position by three bytes.
        /// </summary>
        /// <returns>A 32-bit unsigned integer read from the underlying stream.</returns>
        /// <exception cref="ObjectDisposedException">Method was called after the reader was disposed.</exception>
        /// <exception cref="IOException">An I/O error occurred on the underlying stream.</exception>
        /// <exception cref="NotEnoughBytesException">Not enough bytes in the stream to read a 3-byte DWORD.</exception>
        public unsafe uint Read3ByteDword()
        {
            const int dwordSize = 3;

            byte[] bytes = ReadBytes(dwordSize);
            if (bytes.Length < dwordSize)
                throw new NotEnoughBytesException("Not enough bytes in the stream to read a 3-byte DWORD.", dwordSize, bytes.Length);

            fixed (byte* b = bytes) return (uint)((b[0] << 16) + (b[1] << 8) + b[2]);
        }

        byte[] ReadBytesInternal(int count)
        {
            if (count == 0)
                return EmptyByteArray;

            if (_useBuffering)
                return ReadBytesWithBuffering(count);
            else
                return ReadBytesWithoutBuffering(count);
        }

        byte[] ReadBytesWithBuffering(int count)
        {
            if (!EnsureBufferIsReadyForReading())
                return EmptyByteArray;

            if (_bufferPosition + count <= _bufferSize)
                return ReadBytesFromBuffer(count);

            var availableBytesCount = _bufferSize - _bufferPosition;
            if (availableBytesCount == 0)
                return EmptyByteArray;

            var firstBytes = ReadBytesFromBuffer(availableBytesCount);
            var lastBytes = ReadBytesWithBuffering(count - availableBytesCount);

            var fullBytes = new byte[firstBytes.Length + lastBytes.Length];

            unsafe
            {
                fixed (byte* pFullBytes = fullBytes, pFirstBytes = firstBytes, pLastBytes = lastBytes)
                {
                    var dst = pFullBytes;
                    var src = pFirstBytes;
                    var length = firstBytes.Length;

                    for (var i = 0; i < length; i++) *(dst++) = *(src++);

                    src = pLastBytes;
                    length = lastBytes.Length;

                    for (var i = 0; i < length; i++) *(dst++) = *(src++);
                }
            }

            return fullBytes;
        }

        unsafe byte[] ReadBytesFromBuffer(int count)
        {
            var result = new byte[count];
            unsafe
            {
                fixed (byte* pBuffer = _buffer, pResult = result)
                    Buffer.MemoryCopy(pBuffer + _bufferPosition, pResult, count, count);
            }

            Position += count;
            return result;
        }

        byte[] ReadBytesWithoutBuffering(int count)
        {
            var result = new byte[count];
            var totalReadBytesCount = 0;
            var ptr = 0;

            do
            {
                var readBytesCount = _stream.Read(result, totalReadBytesCount, count);
                if (readBytesCount == 0) break;

                totalReadBytesCount += readBytesCount;
                count -= readBytesCount;

                unsafe
                {
                    fixed (byte* pDest = &result[totalReadBytesCount - readBytesCount], pSrc = &result[ptr])
                        Buffer.MemoryCopy(pSrc, pDest, readBytesCount, readBytesCount);
                }

                ptr += readBytesCount;
            }
            while (count > 0);

            if (totalReadBytesCount != result.Length)
            {
                var copy = new byte[totalReadBytesCount];
                unsafe
                {
                    fixed (byte* pSrc = result, pDest = copy)
                        Buffer.MemoryCopy(pSrc, pDest, totalReadBytesCount, totalReadBytesCount);
                }
                result = copy;
            }

            return result;
        }

        bool EnsureBufferIsReadyForReading()
        {
            if (EndReached)
                return false;

            if (_position < _bufferStart || _position >= _bufferStart + _bufferSize)
            {
                _stream.Position = (_position / _buffer.Length) * _buffer.Length;
                _bufferStart = _stream.Position;

                var totalReadBytesCount = 0;
                var count = _buffer.Length;

                do
                {
                    var readBytesCount = _stream.Read(_buffer, totalReadBytesCount, count);
                    if (readBytesCount == 0)
                        break;

                    totalReadBytesCount += readBytesCount;
                    count -= readBytesCount;
                }
                while (count > 0);

                if (totalReadBytesCount == 0)
                    return false;

                _bufferPosition = (int)(_position % _buffer.Length);
                _bufferSize = totalReadBytesCount;
                return true;
            }

            return _bufferSize > 0;
        }

        void PrepareBuffer()
        {
            if (!_useBuffering) return;

            switch (_settings.BufferingPolicy)
            {
                case BufferingPolicy.BufferAllData:
                    using (var dataStream = new MemoryStream())
                    {
                        _stream.CopyTo(dataStream);
                        _buffer = dataStream.ToArray();
                    }

                    _bufferStart = 0;
                    _bufferSize = _buffer.Length;
                    break;

                case BufferingPolicy.UseFixedSizeBuffer:
                    _buffer = new byte[_settings.BufferSize];
                    break;

                case BufferingPolicy.UseCustomBuffer:
                    if (_settings.Buffer == null)
                        throw new InvalidOperationException($"Buffer is null for {_settings.BufferingPolicy} buffering policy.");
                    
                    _buffer = _settings.Buffer;
                    break;
            }
        }

        #endregion
    }
}