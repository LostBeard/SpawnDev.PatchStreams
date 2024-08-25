namespace SpawnDev.PatchStreams
{
    public class ByteRange
    {
        public long Start { get; private set; }
        public long Size { get; set; }
        /// <summary>
        /// The position immediately after the range data (non-inclusive)
        /// </summary>
        public long EndPos
        {
            get => Start + Size;
            set => Size = value - Start;
        }
        public ByteRange(long start)
        {
            Start = start;
        }
        public ByteRange(long start, long size)
        {
            Start = start;
            Size = size;
        }
    }
}