namespace SpawnDev.PatchStreams
{
    /// <summary>
    /// Patch source information
    /// </summary>
    public class Patch
    {
        /// <summary>
        /// Get or set if this patch is a restore point<br/>
        /// Useful in marking stable points in a stream's patch history
        /// </summary>
        public bool RestorePoint { get; internal set; }
        /// <summary>
        /// Unique patch id useful in representing a specific stream state
        /// </summary>
        public string Id { get; init; }
        /// <summary>
        /// Optional patch description<br/>
        /// Useful for tagging restore points
        /// </summary>
        public string? Description { get; set; }
        /// <summary>
        /// The patches index in the patches collection
        /// </summary>
        public int Index { get; init; }
        /// <summary>
        /// The time the patch was created
        /// </summary>
        public DateTime Created { get; init; }
        /// <summary>
        /// Stream data sources
        /// </summary>
        internal List<Stream> Sources { get; init; }
        /// <summary>
        /// The starting point offset
        /// </summary>
        internal long Offset { get; init; }
        /// <summary>
        /// The stream's size
        /// </summary>
        public long Size { get; init; }
        /// <summary>
        /// Creates a new patch
        /// </summary>
        public Patch(int index, List<Stream> sources, long offset, long size)
        {
            Id = Guid.NewGuid().ToString();
            Index = index;
            Sources = sources;
            Offset = offset;
            Size = size;
            Created = DateTime.Now;
        }
    }
}