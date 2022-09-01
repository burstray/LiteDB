using System;
using System.IO;
using System.Linq;
using static LiteDB.Constants;

namespace LiteDB
{
    public partial class LiteFileStream<TFileId> : Stream
    {
        /// <summary>
        /// Number of bytes on each chunk document to store
        /// </summary>
        public const int MAX_CHUNK_SIZE = 255 * 1024; // 255kb like GridFS

        private readonly ILiteCollection<LiteFileInfo<TFileId>> _files;
        private readonly ILiteCollection<BsonDocument> _chunks;
        private readonly LiteFileInfo<TFileId> _file;
        private readonly BsonValue _fileId;
        private readonly FileAccess _mode;

        private long position = 0;
        private int _currentChunkIndex = 0;
        private byte[] _currentChunkData = null;
        private int _positionInChunk = 0;
        private MemoryStream _buffer;


        //private byte[] buffer;
        private byte[] blankBuffer;
        private bool chunkDirty;
        private long chunkLower = -1;
        private long chunkUpper = -1;
        private int highestBuffPosition;
        private int buffPosition;

        internal LiteFileStream(ILiteCollection<LiteFileInfo<TFileId>> files, ILiteCollection<BsonDocument> chunks, LiteFileInfo<TFileId> file, BsonValue fileId, FileAccess mode)
        {
            _files = files;
            _chunks = chunks;
            _file = file;
            _fileId = fileId;
            _mode = mode;

            if (mode == FileAccess.Read)
            {
                // initialize first data block
                _currentChunkData = this.GetChunkData(_currentChunkIndex);
            }
            else if (mode == FileAccess.Write)
            {
                _buffer = new MemoryStream(MAX_CHUNK_SIZE);

                if (_file.Length > 0)
                {
                    // delete all chunks before re-write
                    var count = _chunks.DeleteMany("_id BETWEEN { f: @0, n: 0 } AND { f: @0, n: 99999999 }", _fileId);

                    ENSURE(count == _file.Chunks);

                    // clear file content length+chunks
                    _file.Length = 0;
                    _file.Chunks = 0;
                }
            }

            //this.buffer = new byte[_currentChunkData.Length];
            this.blankBuffer = new byte[_currentChunkData.Length];
        }

        /// <summary>
        /// Get file information
        /// </summary>
        public LiteFileInfo<TFileId> FileInfo { get { return _file; } }

        public override long Length { get { return _file.Length; } }

        public override bool CanRead { get { return _mode == FileAccess.Read; } }

        public override bool CanWrite { get { return _mode == FileAccess.Write; } }

        public override bool CanSeek { get { return true; } }//Rayx


        #region Dispose

        private bool _disposed = false;

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (_disposed) return;

            if (disposing && this.CanWrite)
            {
                this.Flush();
                _buffer?.Dispose();
            }

            _disposed = true;
        }

        #endregion

        #region Not supported operations

        //public override long Seek(long offset, SeekOrigin origin)
        //{

        //    throw new NotSupportedException();
        //}

        /// <summary>
        /// Seek to any location in the stream.  Seeking past the end of the file is allowed.  Any writes to that
        /// location will cause the file to grow to that size.  Any holes that may be created from the seek will
        /// be zero filled on close.
        /// </summary>
        public override long Seek(long offset, SeekOrigin origin)
        {
            if ((origin < SeekOrigin.Begin) || (origin > SeekOrigin.End))
            {
                throw new ArgumentException("Invalid Seek Origin");
            }

            switch (origin)
            {
                case SeekOrigin.Begin:
                    if (offset < 0)
                    {
                        throw new ArgumentException("Attempted seeking before the begining of the stream");
                    }
                    else
                    {
                        MoveTo(offset);
                    }
                    break;
                case SeekOrigin.Current:
                    MoveTo(position + offset);
                    break;
                case SeekOrigin.End:
                    if (offset <= 0)
                    {
                        throw new ArgumentException("Attempted seeking after the end of the stream");
                    }
                    MoveTo(this.Length - offset);
                    break;
            }
            return position;
        }



        /// <summary>
        /// Moves the current position to the new position.  If this causes a new chunk to need to be loaded it will take
        /// care of flushing the buffer and loading a new chunk.
        /// </summary>
        /// <param name="position">
        /// A <see cref="System.Int32"/> designating where to go to.
        /// </param>
        private void MoveTo(long position)
        {
            Console.WriteLine(_file.Filename+ " MoveTo " + position);

            this.position = position;
            int chunkSize = _currentChunkData.Length;//this.gridFileInfo.ChunkSize;
            bool chunkInRange = (_currentChunkData != null && position >= chunkLower && position < chunkUpper);
            if (chunkInRange == false)
            {
                if (_currentChunkData != null && chunkDirty)
                {
                    highestBuffPosition = Math.Max(highestBuffPosition, buffPosition);
                    this.Flush();
                }
                int chunknum = (int)Math.Floor((double)(position / chunkSize));
                Array.Copy(blankBuffer, _currentChunkData, _currentChunkData.Length);
                //LoadOrCreateChunk(chunknum);
                _currentChunkData = GetChunkData(chunknum);
                //if (_currentChunkData != null)
                //{
                //    byte[] b = (Binary)chunk["data"];
                //    highestBuffPosition = b.Bytes.Length;
                //    Array.Copy(b.Bytes, buffer, highestBuffPosition);
                //}

                chunkDirty = false;
                chunkLower = chunknum * chunkSize;
                chunkUpper = chunkLower + chunkSize;
            }
            buffPosition = (int)(position % chunkSize);
            highestBuffPosition = Math.Max(highestBuffPosition, buffPosition);
        }

        //private long position;
        /// <summary>
        /// When overridden in a derived class, gets or sets the position within the current stream.
        /// </summary>
        /// <value></value>
        /// <returns>
        /// The current position within the stream.
        /// </returns>
        /// <exception cref="T:System.IO.IOException">
        /// An I/O error occurs.
        /// </exception>
        /// <exception cref="T:System.NotSupportedException">
        /// The stream does not support seeking.
        /// </exception>
        /// <exception cref="T:System.ObjectDisposedException">
        /// Methods were called after the stream was closed.
        /// </exception>
        public override long Position
        {
            get { return position; }
            set { this.Seek(value, SeekOrigin.Begin); }
        }

        //public override long Position
        //{
        //    get { return position; }
        //    set { throw new NotSupportedException(); }
        //}
        #endregion

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        ///// <summary>
        ///// Sets the length of this stream to the given value.
        ///// </summary>
        ///// <param name="value">
        ///// A <see cref="System.Int64"/>
        ///// </param>
        //public override void SetLength(long value)
        //{
        //    if (value < 0)
        //        throw new ArgumentOutOfRangeException("length");
        //    if (this.CanSeek == false || this.CanWrite == false)
        //    {
        //        throw new NotSupportedException("The stream does not support both writing and seeking.");
        //    }

        //    if (value < highestPosWritten)
        //    {
        //        TruncateAfter(value);
        //    }
        //    else
        //    {
        //        this.Seek(value, SeekOrigin.Begin);
        //    }
        //    chunkDirty = true;
        //    this.gridFileInfo.Length = value;
        //    highestPosWritten = value;

        //}
    }
}