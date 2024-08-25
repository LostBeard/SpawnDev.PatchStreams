namespace SpawnDev.PatchStreams
{
    public class Range
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
        public Range(long start)
        {
            Start = start;
        }
        public Range(long start, long size)
        {
            Start = start;
            Size = size;
        }
    }
}