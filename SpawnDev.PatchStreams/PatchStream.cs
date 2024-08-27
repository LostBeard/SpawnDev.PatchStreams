using System.Text;

namespace SpawnDev.PatchStreams
{
    /// <summary>
    /// A PatchStream is a writable stream that, when modified, does not modify the underlying data, but instead creates patches to represent data changes<br/>
    /// This is useful if you want to temporarily modify a read a large readonly stream without making a complete copy of it<br/>
    /// As far as the user of the stream is concerned the stream is readable and writable even though any added data is never modified<br/>
    /// All modifications to this stream are saved in patches which can be undone and redone as fast as changing a single integer value<br/>
    /// Restore points can be set at any point and restored easily to make undo/redo easier.<br/>
    /// Supports data deletion, insertion, overwriting, undo/redo, restore points, partial data views, multi-stream read-only sources, and multiple stream insertion.<br/>
    /// Low memory usage and blazing fast modifications.<br/>
    /// IMPORTANT - Once a data has been added to a PatchStream it cannot be modified in any way<br/>
    /// </summary>
    public class PatchStream : Stream
    {
        /// <summary>
        /// Returns a clone of this PatchStream<br/>
        /// All patches are copied. Position, and PatchIndex are also copied so that the current view is retained.<br/>
        /// No underlying data is actually copied, only references are copied.<br/>
        /// Cloning allows forking a stream, where both streams will reference the same data that was available at the time of cloning, but any modifications are independent.
        /// </summary>
        /// <returns></returns>
        public PatchStream Clone()
        {
            var ret = new PatchStream
            {
                Position = Position,
                _Patches = _Patches.ToList(),
                _PatchIndex = _PatchIndex,
            };
            return ret;
        }
        /// <summary>
        /// Fired when restore points are added or removed
        /// </summary>
        public event PatchStreamEvent OnRestorePointsChanged = default!;
        /// <summary>
        /// The patches that make up this stream<br/>
        /// It is critical that the data added to this stream is not modified outside of this object's intended interface.<br/>
        /// </summary>
        public IEnumerable<Patch> Patches => _Patches.ToList();
        private List<Patch> _Patches { get; set; } = new List<Patch>();
        private int _PatchIndex { get; set; } = -1;
        /// <summary>
        /// List of patch ids
        /// </summary>
        public IEnumerable<string> PatchIds => _Patches.Select(o => o.Id).ToList();
        /// <summary>
        /// Current Sources
        /// </summary>
        private List<Stream> Sources => Patch.Sources;
        /// <summary>
        /// Current Patch
        /// </summary>
        public Patch Patch => _Patches[PatchIndex];
        /// <summary>
        /// Get or set if this patch is a restore point<br/>
        /// Useful in marking stable points in a stream's patch history
        /// </summary>
        public bool RestorePoint
        {
            get => Patch.RestorePoint;
            set
            {
                if (Patch.RestorePoint == value || (Patch.Index == 0 && value != true)) return;
                Patch.RestorePoint = value;
                OnRestorePointsChanged?.Invoke(this);
            }
        }
        /// <summary>
        /// Sets the first Patch as active
        /// </summary>
        /// <returns></returns>
        public bool RestoreFirst() => Restore(0);
        /// <summary>
        /// Sets the last Patch as active
        /// </summary>
        /// <returns></returns>
        public bool RestoreLast() => Restore(_Patches.Count - 1);
        /// <summary>
        /// Enable or disable a restore point
        /// </summary>
        /// <param name="patchId">the patch id of the restore point to set</param>
        /// <param name="enable">The value to set</param>
        /// <returns>Returns true if the patch's RestorePoint was found and its value is true</returns>
        public bool SetRestorePoint(string patchId, bool enable)
        {
            var patch = _Patches.FirstOrDefault(o => o.Id == patchId);
            if (patch == null) return false;
            if (patch.RestorePoint != enable)
            {
                patch.RestorePoint = enable;
                OnRestorePointsChanged?.Invoke(this);
            }
            return true;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="includeLastPatch">If true, the last Patch is added to the list (default.)</param>
        /// <returns></returns>
        public IEnumerable<Patch> GetRestorePoints(bool includeLastPatch = true)
        {
            var ret = _Patches.Where(o => o.RestorePoint).ToList();
            if (includeLastPatch)
            {
                var lastPatch = Patches.Last();
                if (!ret.Contains(lastPatch))
                {
                    ret.Add(lastPatch);
                }
            }
            return ret;
        }
        /// <summary>
        /// The active Patch id
        /// </summary>
        public string PatchId => Patch.Id;
        /// <summary>
        /// Segment start position in Source
        /// </summary>
        private long Offset => Patch.Offset;
        /// <summary>
        /// Segment size in bytes.
        /// </summary>
        private long Size => Patch.Size;
        /// <summary>
        /// Current Patch Index
        /// </summary>
        public int PatchIndex => _PatchIndex;
        /// <summary>
        /// Returns true if the current position in the source<br/>
        /// </summary>
        private long SourcePosition { get => Position + Offset; set => Position = value - Offset; }
        /// <summary>
        /// The length of the available data
        /// </summary>
        public override long Length => Size;
        /// <summary>
        /// Returns true if the stream can read<br/>
        /// </summary>
        public override bool CanRead => Position >= 0 && Position < Length;
        /// <summary>
        /// Returns true if the stream can seek<br/>
        /// </summary>
        public override bool CanSeek => Sources != null;
        /// <summary>
        /// Returns true if the stream can write<br/>
        /// </summary>
        public override bool CanWrite => Position >= 0 && Position <= Length;
        /// <summary>
        /// Returns true if the stream can timeout<br/>
        /// </summary>
        public override bool CanTimeout => false;
        /// <summary>
        /// Returns the stream's current position
        /// </summary>
        public override long Position { get; set; } = 0;
        /// <summary>
        /// PatchStreamEvent signature
        /// </summary>
        /// <param name="sender">The event sender</param>
        public delegate void PatchStreamEvent(PatchStream sender);
        /// <summary>
        /// Method signature for the OnChanged event
        /// </summary>
        /// <param name="sender">The event sender</param>
        /// <param name="overwrittenPatches">The patches overwritten by the change, if any</param>
        /// <param name="affectedRegions">The stream regions that were affected by the change</param>
        public delegate void ChangedEvent(PatchStream sender, IEnumerable<Patch> overwrittenPatches, IEnumerable<ByteRange> affectedRegions);
        /// <summary>
        /// Fired when Streams are no longer needed by this stream
        /// </summary>
        /// <param name="sender">The event sender</param>
        /// <param name="discardedStreams">Streams that are no longer needed by this stream</param>
        public delegate void DiscardedStreamsEvent(PatchStream sender, IEnumerable<Stream> discardedStreams);
        /// <summary>
        /// Fired when the stream has been modified
        /// </summary>
        public event ChangedEvent OnChanged = default!;
        /// <summary>
        /// Returns an empty SegmentSource
        /// </summary>
        public static PatchStream Empty => new();
        /// <summary>
        /// The time the current patch was created
        /// </summary>
        public DateTime PatchCreated => Patch.Created;
        /// <summary>
        /// The time the stream was last modified<br/>
        /// Switching patches will update this time to DateTime.Now
        /// </summary>
        public DateTime LastChanged { get; private set; }
        /// <summary>
        /// The time the current patch was created
        /// </summary>
        public DateTime FirstCreated => _Patches.First().Created;
        /// <summary>
        /// Creates an new instance
        /// </summary>
        public PatchStream() => UpdateSource(new Stream[] { new MemoryStream() });
        /// <summary>
        /// Creates an new instance
        /// </summary>
        public PatchStream(IEnumerable<byte[]> source) => UpdateSource(source.Select(o => new MemoryStream(o)));
        /// <summary>
        /// Creates an new instance
        /// </summary>
        public PatchStream(IEnumerable<Stream> source) => UpdateSource(source);
        /// <summary>
        /// Creates an new instance
        /// </summary>
        public PatchStream(Stream source) => UpdateSource(new[] { source });
        /// <summary>
        /// Creates an new instance
        /// </summary>
        public PatchStream(byte[] source) => UpdateSource(new[] { new MemoryStream(source) });
        /// <summary>
        /// Creates an new instance
        /// </summary>
        public PatchStream(IEnumerable<byte[]> source, long offset) => UpdateSource(source.Select(o => new MemoryStream(o)), offset);
        /// <summary>
        /// Creates an new instance
        /// </summary>
        public PatchStream(IEnumerable<Stream> source, long offset) => UpdateSource(source, offset);
        /// <summary>
        /// Creates an new instance
        /// </summary>
        public PatchStream(Stream source, long offset) => UpdateSource(new[] { source }, offset);
        /// <summary>
        /// Creates an new instance
        /// </summary>
        public PatchStream(byte[] source, long offset) => UpdateSource(new[] { new MemoryStream(source) }, offset);
        /// <summary>
        /// Creates an new instance
        /// </summary>
        public PatchStream(IEnumerable<byte[]> source, long offset, long size) => UpdateSource(source.Select(o => new MemoryStream(o)), offset, size);
        /// <summary>
        /// Creates an new instance
        /// </summary>
        public PatchStream(IEnumerable<Stream> source, long offset, long size) => UpdateSource(source, offset, size);
        /// <summary>
        /// Creates an new instance
        /// </summary>
        public PatchStream(Stream source, long offset, long size) => UpdateSource(new[] { source }, offset, size);
        /// <summary>
        /// Creates an new instance
        /// </summary>
        public PatchStream(byte[] source, long offset, long size) => UpdateSource(new[] { new MemoryStream(source) }, offset, size);
        private void UpdateSource(IEnumerable<Stream> source, long offset = 0, long size = -1, long changeOffset = 0, long deletedByteCount = 0, long insertedByteCount = 0, long affectedByteCount = 0)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            var maxSize = source.Sum(o => o.Length);
            if (offset > maxSize) throw new ArgumentOutOfRangeException(nameof(offset));
            if (size < 0) size = maxSize - offset;
            var newList = new List<Stream>();
            if (_PatchIndex == -1 && source.Count() == 1 && source.First().Length == 0)
            {
                newList.AddRange(source);
            }
            else
            {
                foreach (var s in source)
                {
                    var totalSize = newList.Sum(o => o.Length) - offset;
                    if (totalSize >= size) break;
                    // _PatchIndex == 0 sources are allowed to have an empty stream for use if data is Flushed, but only if a single empty source is given
                    if (s == null || s.Length == 0) continue;
                    if (offset > 0 && s.Length <= offset && newList.Count == 0)
                    {
                        offset -= s.Length;
                        continue;
                    }
                    newList.Add(s);
                }
            }
            var patchIndex = _PatchIndex + 1;
            if (patchIndex == 0)
            {
                insertedByteCount = size;
            }
            var patch = new Patch(patchIndex, newList, offset, size, changeOffset, deletedByteCount, insertedByteCount, affectedByteCount);
            var overwritePatchesCount = _Patches.Count - patchIndex;
            var isLatestPatch = overwritePatchesCount == 0;
            if (overwritePatchesCount > 0)
            {
                // discard changed from the current patch index on
                _Patches.RemoveRange(patchIndex, overwritePatchesCount);
            }
            var overwrittenPatches = overwritePatchesCount <= 0 ? Enumerable.Empty<Patch>() : _Patches.Skip(patchIndex).ToList();
            _Patches.Add(patch);
            // the first patch is always marked as a restore point
            IEnumerable<ByteRange> affectedRegions;
            if (patch.Index == 0)
            {
                patch.RestorePoint = true;
                affectedRegions = Enumerable.Empty<ByteRange>();
            }
            else
            {
                // calculate stream data regions that are affected by going from the current patch to 
                affectedRegions = CalculateAffectedRegions(Patch, patch)!;
            }
            // activate the new patch
            _PatchIndex = patchIndex;
            LastChanged = DateTime.Now;
            OnChanged?.Invoke(this, overwrittenPatches, affectedRegions);
            OnRestorePointsChanged?.Invoke(this);
        }
        /// <summary>
        /// Returns a List of non-overlapping ranges representing regions of data that will be affected if switching from [fromPatchId] to [toPatchId]
        /// </summary>
        /// <param name="fromPatchId">The patch to start with</param>
        /// <param name="toPatchId">The patch to end on</param>
        /// <returns>a List of non-overlapping ranges representing regions of data affected by all changes between the specified patches</returns>
        public List<ByteRange>? CalculateAffectedRegions(string fromPatchId, string toPatchId)
        {
            var fromPatch = _Patches.FirstOrDefault(o => o.Id == fromPatchId);
            if (fromPatch == null) return null;
            var toPatch = _Patches.FirstOrDefault(o => o.Id == toPatchId);
            if (toPatch == null) return null;
            return CalculateAffectedRegions(fromPatch, toPatch);
        }
        /// <summary>
        /// Returns a List of non-overlapping ranges representing regions of data that will be affected if switching from [fromPatch] to [toPatch]
        /// </summary>
        /// <param name="fromPatch">The patch to start with</param>
        /// <param name="toPatch">The patch to end on</param>
        /// <returns>a List of non-overlapping ranges representing regions of data affected by all changes between the specified patches</returns>
        public List<ByteRange>? CalculateAffectedRegions(Patch fromPatch, Patch toPatch)
        {
            var ret = new List<ByteRange>();
            if (fromPatch == toPatch) return ret;
            var startI = _Patches.IndexOf(fromPatch);
            var endI = _Patches.IndexOf(toPatch);
            // if either is no longer in the Patches list return null
            if (startI == -1 || endI == -1) return null;
            if (startI < endI)
            {
                // ranges from every patch starting at [from] + 1 -> [to] are collected
                for (var i = startI + 1; i <= endI; i++)
                {
                    var patch = _Patches[i];
                    var range = new ByteRange(patch.ChangeOffset, patch.AffectedByteCount);
                    ret.Add(range);
                }
            }
            else
            {
                // ranges from every patch starting at [from] -> [to + 1] are collected
                for (var i = startI; i > endI; i--)
                {
                    var patch = _Patches[i];
                    var range = new ByteRange(patch.ChangeOffset, patch.AffectedByteCount);
                    ret.Add(range);
                }
            }
            // ranges have to be sorted before merging
            ret = ret.OrderBy(o => o.Start).ToList();
            // merge ranges
            var tmp = new List<ByteRange>();
            foreach (var item in ret)
            {
                var last = tmp.LastOrDefault();
                if (last == null)
                {
                    tmp.Add(item);
                }
                else
                {
                    if (item.Start < last.EndPos)
                    {
                        last.EndPos = Math.Max(last.EndPos, item.EndPos);
                    }
                    else
                    {
                        tmp.Add(item);
                    }
                }
            }
            // return non-overlapping affected regions
            return tmp;
        }
        /// <summary>
        /// Set the active patch by patchId
        /// </summary>
        /// <param name="patchId"></param>
        /// <returns></returns>
        public bool Restore(string patchId)
        {
            var patch = _Patches.FirstOrDefault(o => o.Id == patchId);
            if (patch == null) return false;
            var patchIndex = _Patches.IndexOf(patch);
            return Restore(patchIndex);
        }
        /// <summary>
        /// Set the active patch by index
        /// </summary>
        /// <param name="patchIndex"></param>
        /// <returns></returns>
        public bool Restore(int patchIndex)
        {
            if (patchIndex < 0 || patchIndex >= _Patches.Count) return false;
            if (_PatchIndex == patchIndex)
            {
                return true;
            }
            var patch = _Patches[patchIndex];
            var affectedRegions = CalculateAffectedRegions(Patch, patch);
            _PatchIndex = patchIndex;
            LastChanged = DateTime.Now;
            OnChanged?.Invoke(this, Enumerable.Empty<Patch>(), affectedRegions!);
            return true;
        }
        /// <summary>
        /// Move to the previous Patch with RestorePoint == true in Patches, if there is one.<br/>
        /// The first patch and last patch are both considered un-official restore points.
        /// </summary>
        /// <returns></returns>
        public bool RestorePointUndo()
        {
            // goes to the previous restore point
            var restorePoints = GetRestorePoints();
            var lastRestorePointBeforeThisPatch = restorePoints.Where(o => o.Index < PatchIndex).LastOrDefault();
            if (lastRestorePointBeforeThisPatch == null) return false;
            return Restore(lastRestorePointBeforeThisPatch.Index);
        }
        /// <summary>
        /// Move to the next Patch with RestorePoint == true in Patches, if there is one.<br/>
        /// The first patch and last patch are both considered un-official restore points.
        /// </summary>
        /// <returns></returns>
        public bool RestorePointRedo()
        {
            // goes to the next restore point or the last patch if no restore points are after the current patch
            var restorePoints = GetRestorePoints();
            var lastRestorePointBeforeThisPatch = restorePoints.Where(o => o.Index > PatchIndex).FirstOrDefault();
            if (lastRestorePointBeforeThisPatch == null) return false;
            return Restore(lastRestorePointBeforeThisPatch.Index);
        }
        /// <summary>
        /// Move to the previous Patch in Patches, if there is one
        /// </summary>
        /// <returns></returns>
        public bool Undo() => Restore(PatchIndex - 1);
        /// <summary>
        /// Move to the next Patch in Patches, if there is one
        /// </summary>
        /// <returns></returns>
        public bool Redo() => Restore(PatchIndex + 1);
        /// <summary>
        /// Returns true if there is previous Patch
        /// </summary>
        public bool CanUndo => PatchIndex > 0;
        /// <summary>
        /// Returns true if there is a Patch following the active one in Patches
        /// </summary>
        public bool CanRedo => PatchIndex < _Patches.Count - 1;
        /// <summary>
        /// Returns a streams sources that are used by this streams patches
        /// </summary>
        /// <returns></returns>
        public IEnumerable<Stream> GetUniqueStreams()
        {
            var streams = _Patches.SelectMany(o => o.Sources).Distinct();
            return streams;
        }
        /// <summary>
        /// Returns the number of bytes that can be flushed to the original stream.<br/>
        /// PatchStream must have been created with a single, writable stream.
        /// </summary>
        /// <returns>The number of bytes that will be written to the original stream</returns>
        public long CanFlush()
        {
            if (_Patches.Count < 2) return 0;
            if (_PatchIndex == 0) return 0;
            // When flush is called, check if the first Patch is a single, Writable stream.
            // If it is, get all affected regions from the current patch to the first
            // then copy all affected regions to the first
            var originalPatch = Patches.First();
            var originalSources = originalPatch.Sources;
            if (originalSources.Count != 1) return 0;
            var destinationStream = originalSources[0];
            // the destination stream must be writable
            if (!destinationStream.CanWrite) return 0;
            var affectedRegions = CalculateAffectedRegions(Patch, originalPatch);
            return affectedRegions == null  ? 0 : affectedRegions.Sum(o => o.Size);
        }
        /// <summary>
        /// Flush all patches from the current patch to the first to the underlying source if the original source is a single, writable stream<br/>
        /// Because we have modified an underlying source, all Patches will be discarded.<br/>
        /// As far as any viewers of this stream are concerned, no data has changed<br/>
        /// This cannot be undone.
        /// </summary>
        public override void Flush()
        {
#if !DEBUG
            throw new NotImplementedException("Incomplete. Will be enabled in a future release.");
#endif
            if (_Patches.Count < 2) return;
            if (_PatchIndex == 0) return;
            // When flush is called, check if the first Patch is a single, Writable stream.
            // If it is, get all affected regions from the current patch to the first
            // then copy all affected regions to the first
            var originalPatch = Patches.First();
            var originalSources = originalPatch.Sources;
            if (originalSources.Count != 1) return;
            var destinationStream = originalSources[0];
            // the destination stream must be writable
            if (!destinationStream.CanWrite) return;
            var affectedRegions = CalculateAffectedRegions(Patch, originalPatch);
            if (affectedRegions == null) return;
            var destinationOffset = originalPatch.Offset;
            var destinationPosition = destinationStream.Position;
            var originalPosition = Position;
            // copy regions into destinationStream starting at destinationOffset
            foreach (var region in affectedRegions)
            {
                Position = region.Start;
                destinationStream.Position = region.Start + destinationOffset;
                CopyStream(destinationStream, (int)region.Size);
            }
            // verify stream length is correct
            if (destinationStream.Length != Length + destinationOffset)
            {
                destinationStream.SetLength(Length + destinationOffset);
            }
            // because we have modified an underlying source, all Patches, except the first, will be discarded
            // as far as any viewers of this stream are concerned, no data has changed
            _PatchIndex = -1;
            destinationStream.Position = destinationPosition;
            Position = originalPosition;
            UpdateSource(originalPatch.Sources, originalPatch.Offset);
        }
        /// <summary>
        /// Flush all patches from the current patch to the first to the underlying source if the original source is a single, writable stream<br/>
        /// Because we have modified an underlying source, all Patches will be discarded.<br/>
        /// As far as any viewers of this stream are concerned, no data has changed<br/>
        /// This cannot be undone.
        /// </summary>
        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
#if !DEBUG
            throw new NotImplementedException("Incomplete. Will be enabled in a future release.");
#endif
            if (_Patches.Count < 2) return;
            if (_PatchIndex == 0) return;
            // When flush is called, check if the first Patch is a single, Writable stream.
            // If it is, get all affected regions from the current patch to the first
            // then copy all affected regions to the first
            var originalPatch = Patches.First();
            var originalSources = originalPatch.Sources;
            if (originalSources.Count != 1) return;
            var destinationStream = originalSources[0];
            // the destination stream must be writable
            if (!destinationStream.CanWrite) return;
            var affectedRegions = CalculateAffectedRegions(Patch, originalPatch);
            if (affectedRegions == null) return;
            var destinationOffset = originalPatch.Offset;
            var destinationPosition = destinationStream.Position;
            var originalPosition = Position;
            // copy regions into destinationStream starting at destinationOffset
            foreach (var region in affectedRegions)
            {
                Position = region.Start;
                destinationStream.Position = region.Start + destinationOffset;
                await CopyStreamAsync(destinationStream, (int)region.Size, cancellationToken);
            }
            // verify stream length is correct
            if (destinationStream.Length != Length + destinationOffset)
            {
                destinationStream.SetLength(Length + destinationOffset);
            }
            // because we have modified an underlying source, all Patches, except the first, will be discarded
            // as far as any viewers of this stream are concerned, no data has changed
            _PatchIndex = -1;
            destinationStream.Position = destinationPosition;
            Position = originalPosition;
            UpdateSource(originalPatch.Sources, originalPatch.Offset);
        }
        /// <summary>
        /// Copies the specified number of bytes from this stream, starting at the specified position, to the destination stream
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="bytes"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task CopyStreamAsync(Stream destination, int bytes, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[81920];
            int read;
            while (bytes > 0 && (read = await ReadAsync(buffer, 0, Math.Min(buffer.Length, bytes), cancellationToken)) > 0)
            {
                await destination.WriteAsync(buffer, 0, read, cancellationToken);
                bytes -= read;
            }
        }
        /// <summary>
        /// Copies the specified number of bytes from this stream, starting at the specified position, to the destination stream
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public void CopyStream(Stream destination, int bytes)
        {
            byte[] buffer = new byte[81920];
            int read;
            while (bytes > 0 && (read = Read(buffer, 0, Math.Min(buffer.Length, bytes))) > 0)
            {
                destination.Write(buffer, 0, read);
                bytes -= read;
            }
        }
        /// <summary>
        /// Not implemented
        /// </summary>
        public override void SetLength(long value) => throw new NotImplementedException();
        /// <summary>
        /// Copy data from buffer to this stream
        /// </summary>
        /// <param name="buffer">The data source</param>
        /// <param name="offset">The offset in buffer to start copying from</param>
        /// <param name="count">The number of bytes to copy from buffer to this stream</param>
        /// <exception cref="Exception">Thrown if Position > stream Length</exception>
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (Position > Length) throw new Exception("Write past end of file");
            var bytes = buffer.Skip(offset).Take(count).ToArray();
            var overwriteCount = InsertWrites ? 0 : count;
            Splice(Position, overwriteCount, new MemoryStream(bytes));
            Position += count;
        }
        /// <summary>
        /// When true, writes will insert data instead writing over data
        /// </summary>
        public bool InsertWrites { get; set; }
        /// <summary>
        /// Set the stream's position
        /// </summary>
        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = offset;
                    break;
                case SeekOrigin.End:
                    Position = Length + offset;
                    break;
                case SeekOrigin.Current:
                    Position = Position + offset;
                    break;
            }
            return Position;
        }
        /// <summary>
        /// Returns a new instance of this type with as few of the Source streams needed as possible, to represent the given slice
        /// </summary>
        /// <param name="start">The position to start taking data from</param>
        /// <param name="size">The amount of data to copy from this stream</param>
        /// <returns></returns>
        public PatchStream Slice(long start, long size)
        {
            //var source = Sources;
            //var offset = start + Offset;
            //var newList = new List<Stream>();
            //foreach (var s in source)
            //{
            //    if (s == null || s.Length == 0) continue;
            //    if (offset > 0 && s.Length < offset)
            //    {
            //        offset -= s.Length;
            //        continue;
            //    }
            //    newList.Add(s);
            //    var totalSize = newList.Sum(o => o.Length) - offset;
            //    if (totalSize >= size) break;
            //}
            return new PatchStream(Sources, start + Offset, size);
        }
        /// <summary>
        /// Returns a new MultiStreamSegment based on this SegmentSource, optionally with data removed, replaced, or inserted as specified<br/>
        /// This streams Position is restored before this method returns
        /// </summary>
        /// <param name="start">The position start start adding the data from addStreams, and the position to replace the specified amount of data</param>
        /// <param name="replaceLength">The number of bytes to replace. -1 can be used to indicate no data from start on will be used</param>
        /// <param name="addStreams">Streams to add at start position</param>
        /// <returns>A new MultiStreamSegment</returns>
        public PatchStream ToSpliced(long start, long replaceLength, params Stream[] addStreams)
        {
            long pos = 0;
            var streams = new List<Stream>();
            if (start > 0)
            {
                var preSlice = Slice(0, start);
                streams.Add(preSlice);
                if (replaceLength < 0)
                {
                    pos = Length;
                }
            }
            pos = start + replaceLength;
            // insert streams if any
            if (addStreams != null) streams.AddRange(addStreams);
            // add anything left in this source
            if (pos < Length)
            {
                streams.Add(Slice(pos, Length - pos));
            }
            return new PatchStream(streams);
        }
        /// <summary>
        /// Delete [length] bytes of data from the stream starting at the position [start]<br/>
        /// This will reduce the streams length by the number of bytes deleted
        /// </summary>
        /// <param name="start">The position to start deleting from</param>
        /// <param name="length">How much data to delete</param>
        /// <returns></returns>
        public long Delete(long start, long length) => Splice(start, length, Array.Empty<Stream>());
        /// <summary>
        /// Delete [length] bytes of data from the stream at the current Position<br/>
        /// This will reduce the streams length by the number of bytes deleted
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        public long Delete(long length)
        {
            if (length < 0) length = Length;
            var bytesLeft = Length - Position;
            length = Math.Min(length, bytesLeft);
            if (length > 0) Splice(Position, length, Array.Empty<Stream>());
            return length;
        }
        /// <summary>
        /// Deletes all stream data<br/>
        /// As with all PatchStream operations, this is undoable and no data is actualyl deleted.
        /// </summary>
        /// <returns></returns>
        public long Delete()
        {
            Position = 0;
            return Delete(-1);
        }
        /// <summary>
        /// Insert data into this stream<br/>
        /// A new patch will be created, and any patch data after the current patch will be discarded
        /// </summary>
        /// <param name="data">The data to write</param>
        /// <param name="replaceLength">The amount of data to overwrite. Default is 0</param>
        /// <returns>The number of bytes written</returns>
        public long Insert(Stream data, long replaceLength = 0)
        {
            var dataSize = data.Length;
            Splice(Position, replaceLength, data);
            Position += dataSize;
            return dataSize;
        }
        /// <summary>
        /// Writes data at the current position<br/>
        /// </summary>
        /// <param name="data"></param>
        /// <param name="replaceLength"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        public long Write(string data, long replaceLength = 0, Encoding? encoding = null)
        {
            var bytes = (encoding ?? Encoding.UTF8).GetBytes(data);
            Write(bytes, 0, bytes.Length);
            return bytes.Length;
        }
        /// <summary>
        /// Insert data into this stream<br/>
        /// A new patch will be created, and any patch data after the current patch will be discarded
        /// </summary>
        /// <param name="data">The data to write</param>
        /// <param name="replaceLength">The amount of data to overwrite. Default is 0</param>
        /// <param name="encoding">The text encoding to use. Default is UTF8</param>
        /// <returns>The number of bytes written</returns>
        public long Insert(string data, long replaceLength = 0, Encoding? encoding = null) => Insert((encoding ?? Encoding.UTF8).GetBytes(data), replaceLength);
        /// <summary>
        /// Insert data into this stream<br/>
        /// A new patch will be created, and any patch data after the current patch will be discarded
        /// </summary>
        /// <param name="data">The data to write</param>
        /// <param name="replaceLength">The amount of data to overwrite. Default is 0</param>
        /// <returns>The number of bytes written</returns>
        public long Insert(byte[] data, long replaceLength = 0) => Insert(new MemoryStream(data), replaceLength);
        /// <summary>
        /// Insert data into this stream<br/>
        /// A new patch will be created, and any patch data after the current patch will be discarded
        /// </summary>
        /// <param name="streams">The data to write</param>
        /// <param name="replaceLength">The amount of data to overwrite. Default is 0</param>
        /// <returns>The number of bytes written</returns>
        public long Insert(IEnumerable<Stream> streams, long replaceLength = 0)
        {
            var data = streams.ToArray();
            var dataSize = data.Sum(o => o.Length);
            Splice(Position, replaceLength, data);
            Position += dataSize;
            return dataSize;
        }
        /// <summary>
        /// Insert data into this stream<br/>
        /// A new patch will be created, and any patch data after the current patch will be discarded
        /// </summary>
        /// <param name="data">The data to write</param>
        /// <param name="replaceLength">The amount of data to overwrite. Default is 0</param>
        /// <returns>The number of bytes written</returns>
        public long Insert(IEnumerable<byte[]> data, long replaceLength = 0) => Insert(data.Select(o => new MemoryStream(o)), replaceLength);
        /// <summary>
        /// Write 0 or more bytes to the stream starting at position [start] and overwriting [replaceLength] bytes
        /// </summary>
        /// <param name="start">The position to start writing to</param>
        /// <param name="replaceLength">The amount of data to overwrite</param>
        /// <param name="addBytes">The data to write</param>
        /// <returns>The number of bytes written</returns>
        public long Splice(long start, long replaceLength, params byte[][] addBytes) => Splice(start, replaceLength, addBytes.Select(o => new MemoryStream(o)).ToArray());
        /// <summary>
        /// Write 0 or more bytes to the stream starting at position [start] and overwriting [replaceLength] bytes
        /// </summary>
        /// <param name="start">The position to start writing to</param>
        /// <param name="deleteCount">
        /// An integer indicating the number of elements in the array to remove from start.<br/>
        /// If deleteCount &lt; 0, all data from `start` to the end of the stream will be deleted.<br/>
        /// If deleteCount == 0, no data will be deleted. In this case, you should specify at least one new element.
        /// </param>
        /// <param name="addStreams">The data to write</param>
        /// <returns>The number of bytes written</returns>
        public long Splice(long start, long deleteCount, params Stream[] addStreams)
        {
            if (start > Length || start < 0) throw new ArgumentOutOfRangeException(nameof(start));
            var deleteCountMax = Length - start;
            if (deleteCount < 0) deleteCount = deleteCountMax;
            if (deleteCount > deleteCountMax) deleteCount = deleteCountMax;
            long insertCount = addStreams?.Sum(o => o.Length) ?? 0;
            if (insertCount == 0 && deleteCount == 0)
            {
                // nothing changed.
                return 0;
            }
            long pos = 0;
            var streams = new List<Stream>();
            if (start > 0)
            {
                var preSlice = Slice(0, start);
                streams.Add(preSlice);
                if (deleteCount < 0)
                {
                    pos = Length;
                }
            }
            pos = start + deleteCount;
            // add insert streams if any
            if (addStreams != null)
            {
                streams.AddRange(addStreams);
            }
            // add anything left in this source
            if (pos < Length)
            {
                var endSlice = Slice(pos, Length - pos);
                streams.Add(endSlice);
            }
            // get the number of bytes in the current stream state that are affected by this change
            // if delete count == insert count
            // then affected = delete count
            // else affected = max(new length, old length) - start
            var newSize = Length - deleteCount + insertCount;
            var affectedMax = Math.Max(newSize, Length) - start;
            var affectedBytes = deleteCount == insertCount ? insertCount : affectedMax;
            UpdateSource(streams, 0, -1, start, deleteCount, insertCount, affectedBytes);
            return insertCount;
        }
        /// <summary>
        /// Read data from the underlying source
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var bytesLeftInSegment = Length - Position;
            count = (int)Math.Min(count, bytesLeftInSegment);
            if (count <= 0) return 0;
            var sourceIndex = 0;
            var source = Sources[sourceIndex];
            var currentOffset = SourcePosition;
            while (source.Length < currentOffset)
            {
                if (sourceIndex >= source.Length - 1) return 0;
                sourceIndex++;
                currentOffset = currentOffset - source.Length;
                source = Sources[sourceIndex];
            }
            int bytesRead = 0;
            int bytesLeft = count;
            var bytesReadTotal = 0;
            var positions = Sources.Select(o => o.Position).ToArray();
            source.Position = currentOffset;
            while (sourceIndex < Sources.Count && bytesLeft > 0)
            {
                var sourceBytesLeft = source.Length - source.Position;
                while (sourceBytesLeft <= 0)
                {
                    if (sourceIndex >= Sources.Count - 1) goto LoopEnd;
                    sourceIndex++;
                    source = Sources[sourceIndex];
                    source.Position = 0;
                    sourceBytesLeft = source.Length;
                }
                var readByteCount = (int)Math.Min(bytesLeft, sourceBytesLeft);
                bytesRead = await source.ReadAsync(buffer, bytesReadTotal + offset, readByteCount, cancellationToken);
                bytesReadTotal += (int)bytesRead;
                bytesLeft -= bytesRead;
                if (bytesRead <= 0 || bytesLeft <= 0) break;
            }
        LoopEnd:
            SourcePosition += bytesReadTotal;
            // restore stream positions
            for (var i = 0; i < Sources.Count; i++)
            {
                Sources[i].Position = positions[i];
            }
            return bytesReadTotal;
        }
        /// <summary>
        /// Read data from the stream starting at the current position
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            var bytesLeftInSegment = Length - Position;
            count = (int)Math.Min(count, bytesLeftInSegment);
            if (count <= 0) return 0;
            var sourceIndex = 0;
            var source = Sources[sourceIndex];
            var currentOffset = SourcePosition;
            while (source.Length < currentOffset)
            {
                if (sourceIndex >= source.Length - 1) return 0;
                sourceIndex++;
                currentOffset = currentOffset - source.Length;
                source = Sources[sourceIndex];
            }
            int bytesRead = 0;
            int bytesLeft = count;
            var bytesReadTotal = 0;
            var positions = Sources.Select(o => o.Position).ToArray();
            source.Position = currentOffset;
            while (sourceIndex < Sources.Count && bytesLeft > 0)
            {
                var sourceBytesLeft = source.Length - source.Position;
                while (sourceBytesLeft <= 0)
                {
                    if (sourceIndex >= Sources.Count - 1) goto LoopEnd;
                    sourceIndex++;
                    source = Sources[sourceIndex];
                    source.Position = 0;
                    sourceBytesLeft = source.Length;
                }
                var readByteCount = (int)Math.Min(bytesLeft, sourceBytesLeft);
                bytesRead = source.Read(buffer, bytesReadTotal + offset, readByteCount);
                bytesReadTotal += (int)bytesRead;
                bytesLeft -= bytesRead;
                if (bytesRead <= 0 || bytesLeft <= 0) break;
            }
        LoopEnd:
            SourcePosition += bytesReadTotal;
            // restore stream positions
            for (var i = 0; i < Sources.Count; i++)
            {
                Sources[i].Position = positions[i];
            }
            return bytesReadTotal;
        }
        /// <summary>
        /// Copy this stream starting at the current position into a new byte array
        /// </summary>
        /// <returns></returns>
        public byte[] ToArray()
        {
            var ret = new byte[Length - Position];
            _ = Read(ret, 0, ret.Length);
            return ret;
        }
        /// <summary>
        /// Returns the stream as a string starting from the current position or the beginning if fullBuffer is true<br/>
        /// Position is modified if fullBuffer == false
        /// </summary>
        /// <returns></returns>
        public string ToString(bool fullBuffer)
        {
            return Encoding.UTF8.GetString(ToArray(fullBuffer));
        }
        /// <summary>
        /// Returns the stream as a byte array starting from the current position or the beginning if fullBuffer is true<br/>
        /// Position is modified if fullBuffer == false
        /// </summary>
        /// <returns></returns>
        public byte[] ToArray(bool fullBuffer)
        {
            if (!fullBuffer) return ToArray();
            var pos = Position;
            Position = 0;
            var ret = ToArray();
            Position = pos;
            return ret;
        }
    }
}