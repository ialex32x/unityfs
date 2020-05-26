using System;
using System.IO;
using System.Security.Cryptography;

namespace UnityFS.Utils
{
    public class ChunkedStream : Stream
    {
        private long _position;
        private long _rsize;
        private bool _canRead;
        private byte[] _chunk;
        private long _lastChunkBegin;
        private int _lastChunkRSize;

        private bool _leaveOpen;
        private Stream _stream;
        private ICryptoTransform _transform;

        public override bool CanRead => _canRead;

        public override bool CanSeek => _canRead;

        public override bool CanWrite => false;

        public override long Length => _rsize;

        public override long Position
        {
            get => _position;
            set
            {
                if (_position != value)
                {
                    if (value < 0 || value > _rsize)
                    {
                        throw new IndexOutOfRangeException();
                    }

                    _position = value;
                }
            }
        }

        public static int GetChunkSize(int chunkSize)
        {
            if (chunkSize < 512)
            {
                return 512;
            }

            return chunkSize - chunkSize % 512;
        }

        public static ICryptoTransform CreateDecryptor(byte[] key, byte[] iv)
        {
            var algo = Rijndael.Create();
            algo.Padding = PaddingMode.Zeros;
            var decryptor = algo.CreateDecryptor(key, iv);
            return decryptor;
        }

        public ChunkedStream(byte[] key, byte[] iv, Stream stream, long rsize, int chunkSize = 4096,
            bool leaveOpen = false)
            : this(CreateDecryptor(key, iv), stream, rsize, chunkSize, leaveOpen)
        {
        }

        public ChunkedStream(ICryptoTransform transform, Stream stream, long rsize, int chunkSize = 4096,
            bool leaveOpen = false)
        {
            if (!stream.CanRead || !stream.CanSeek)
            {
                throw new ArgumentException("Stream must CanRead && CanSeek");
            }

            _chunk = new byte[GetChunkSize(chunkSize)];
            _lastChunkBegin = -1;
            _lastChunkRSize = 0;
            _transform = transform;
            _leaveOpen = leaveOpen;
            _canRead = true;
            _stream = stream;
            _position = 0;
            _rsize = rsize;
            _stream.Seek(0, SeekOrigin.Begin);
        }

        public static void Encrypt(ICryptoTransform transform, int chunkSize, byte[] original, Stream outStream)
        {
            var chunk = new byte[GetChunkSize(chunkSize)];
            var chunkCount = original.Length / chunk.Length;
            for (var i = 0; i <= chunkCount; i++)
            {
                var chunkBegin = i * chunk.Length;
                var chunkRSize = Math.Min(original.Length - chunkBegin, chunk.Length);
                if (chunkRSize > 0)
                {
                    var outBuffer = transform.TransformFinalBlock(original, chunkBegin, chunkRSize);

                    outStream.Write(outBuffer, 0, outBuffer.Length);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (!disposing)
                {
                    return;
                }

                if (_leaveOpen)
                {
                    return;
                }

                _stream.Close();
            }
            finally
            {
                try
                {
                    _canRead = false;
                }
                finally
                {
                    base.Dispose(disposing);
                }
            }
        }

        public override void Flush()
        {
        }

        private int ReadChunk(long chunkBegin)
        {
            if (chunkBegin == _lastChunkBegin)
            {
                return _lastChunkRSize;
            }

            _lastChunkBegin = chunkBegin;
            if (_stream.Position != chunkBegin)
            {
                _stream.Seek(chunkBegin, SeekOrigin.Begin);
            }

            var read = _stream.Read(_chunk, 0, _chunk.Length);
            if (read == 0)
            {
                _lastChunkRSize = 0;
                return 0;
            }

            var block = _transform.TransformFinalBlock(_chunk, 0, read);
            _lastChunkRSize = block.Length;
            Buffer.BlockCopy(block, 0, _chunk, 0, _lastChunkRSize);
            return _lastChunkRSize;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position >= _rsize)
            {
                return 0;
            }

            var chunkSize = _chunk.Length; // 分段容量
            var fread = 0; // 最终实际读取字节数
            var chunkOffset = _position % chunkSize; // 分段内的偏移值
            var chunkBegin = _position - chunkOffset; // 分段相对文件流开始的偏移值

            while (fread < count)
            {
                var chunkRead = (int) (chunkSize - chunkOffset);
                var nextPosition = _position + chunkRead;
                var chunkRSize = ReadChunk(chunkBegin);

                if (chunkRSize == 0)
                {
                    break;
                }

                if (nextPosition >= _rsize)
                {
                    var tailRead = Math.Min((int) (_rsize - _position), count - fread);
                    Buffer.BlockCopy(_chunk, (int) chunkOffset, buffer, offset, tailRead);
                    _position += tailRead;
                    fread += tailRead;
                    break;
                }
                else
                {
                    var tailRead = Math.Min(chunkRead, count - fread);
                    Buffer.BlockCopy(_chunk, (int) chunkOffset, buffer, offset, tailRead);

                    _position += tailRead;
                    offset += tailRead;
                    chunkOffset = 0;
                    chunkBegin += chunkSize;
                    fread += tailRead;
                }
            }

            return fread;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = offset;
                    break;
                case SeekOrigin.Current:
                    Position = _position + offset;
                    break;
                case SeekOrigin.End:
                    Position = _rsize - offset;
                    break;
                default: throw new ArgumentException();
            }

            return _position;
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}