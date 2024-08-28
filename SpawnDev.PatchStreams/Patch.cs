namespace SpawnDev.PatchStreams
{
    /// <summary>
    /// Patch source information
    /// </summary>
    public class Patch
    {
        PatchStream? _SnapShot = null;
        /// <summary>
        /// Returns a snapshot for this Patch
        /// </summary>
        public PatchStream SnapShot => _SnapShot ??= new PatchStream(this);
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
        //public int Index { get; init; }
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
        /// The position at which changes made by this patch start<br/>
        /// This and change start pos contains information about what data a change has affected
        /// </summary>
        public long ChangeOffset { get; init; }
        /// <summary>
        /// The number of bytes deleted from the stream starting at position [ChangeOffset] before data was added (if any)
        /// </summary>
        public long DeletedByteCount { get; init; }
        /// <summary>
        /// The number of bytes inserted into the stream starting at position [ChangeOffset] after [DeletedByteCount] bytes were deleted
        /// </summary>
        public long InsertedByteCount { get; init; }
        /// <summary>
        /// The number of bytes that this patch affected in the previous patch
        /// </summary>
        public long AffectedByteCount { get; init; }
        /// <summary>
        /// Creates a new patch
        /// </summary>
        public Patch(List<Stream> sources, long offset, long size, long changeOffset, long deletedByteCount, long insertedByteCount, long affectedByteCount)
        {
            Id = Guid.NewGuid().ToString();
            Sources = sources;
            Offset = offset;
            Size = size;
            Created = DateTime.Now;
            ChangeOffset = changeOffset;
            DeletedByteCount = deletedByteCount;
            InsertedByteCount = insertedByteCount;
            AffectedByteCount = affectedByteCount;
        }
    }
}