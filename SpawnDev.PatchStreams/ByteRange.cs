namespace SpawnDev.PatchStreams
{
    /// <summary>
    /// Represents a region of stream
    /// </summary>
    public class ByteRange
    {
        /// <summary>
        /// The start position
        /// </summary>
        public long Start { get; private set; }
        /// <summary>
        /// The byte size of the range
        /// </summary>
        public long Size { get; set; }
        /// <summary>
        /// The position immediately after the range data (non-inclusive)
        /// </summary>
        public long EndPos
        {
            get => Start + Size;
            set => Size = value - Start;
        }
        /// <summary>
        /// Creates a new range
        /// </summary>
        /// <param name="start">range start</param>
        /// <param name="size">range size</param>
        public ByteRange(long start, long size = 0)
        {
            Start = start;
            Size = size;
        }
    }
}