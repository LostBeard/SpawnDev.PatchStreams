# SpawnDev.PatchStreams

[![NuGet version](https://badge.fury.io/nu/SpawnDev.PatchStreams.svg)](https://www.nuget.org/packages/SpawnDev.PatchStreams)

A patch-based `Stream` for non-destructive editing. Writes never touch the source bytes — they compose a virtual view of one or more underlying streams (or byte ranges within them) that callers can read as if it were a single stream. Every edit becomes a patch: cheap to record, cheap to undo, cheap to redo.

Targets: `net10.0`, `net9.0`, `net8.0`.

## Why

Some workloads need to edit a stream but can't afford to copy it — a large file, a parsed media container (e.g. WebM), or a stream whose source data is effectively read-only. Traditional stream editing means buffering and rewriting. PatchStreams stitches together views instead, so edits are O(1) and the source bytes stay untouched until (optionally) flushed.

## Core ideas

- **A PatchStream is a virtual view composed of slices from one or more source streams, presented as one continuous `Stream`.** Reads walk across those slices transparently.
- **Edits create patches, not byte copies.** An `Insert`, `Delete`, `Splice`, or `Move` becomes a new patch that describes which slices to present next. The actual source bytes never move.
- **Undo/redo is just switching which patch is active.** Walking the patch list is constant time; no data is copied.
- **Restore points** mark stable intermediate states in the patch list — useful for grouping several low-level edits into one logical checkpoint that can be jumped back to.
- **`LatestStable` is the snapshot a reader should trust.** It walks back from the current patch to the most recent restore point and returns a frozen `PatchStream` of that state. Useful when a writer is mid-edit and a concurrent reader wants a coherent view.

## Quick tour

```cs
using SpawnDev.PatchStreams;

// Create from a source stream, a byte[], or nothing.
var patchStream = new PatchStream(new MemoryStream());

// Default Write is overwrite (like File streams).
patchStream.Write("world!");                // stream is "world!"

// Insert mode prepends without overwriting.
patchStream.InsertWrites = true;
patchStream.Position = 0;
patchStream.Write("Hello ");                // stream is "Hello world!"

// Mark a stable checkpoint.
patchStream.RestorePoint = true;

// Overwrite a region.
patchStream.InsertWrites = false;
patchStream.Position = 6;
patchStream.Write("DotNet!");               // stream is "Hello DotNet!"

// Insert more in front.
patchStream.InsertWrites = true;
patchStream.Position = 0;
patchStream.Write("Presenting: ");          // stream is "Presenting: Hello DotNet!"

// Wipe it all.
patchStream.Delete();                        // stream is ""

// Undo just the Delete.
patchStream.Undo();                          // stream is "Presenting: Hello DotNet!"

// Jump back to the last restore point.
patchStream.RestorePointUndo();              // stream is "Hello world!"

// Optional: write the current patched state back to the original
// writable source. Irreversible.
patchStream.Flush();
```

## Operations

| Method | Effect |
|---|---|
| `Write(byte[], int, int)` | Overwrite or insert (controlled by `InsertWrites`). |
| `Insert(byte[] / Stream / IEnumerable<Stream>)` | Insert at current `Position`. |
| `Delete(start, length)` / `Delete(length)` / `Delete()` | Remove a range (or everything). |
| `Splice(start, deleteCount, params Stream[])` | Atomic delete + insert. |
| `Slice(start, size)` | Returns a new `PatchStream` view into this one without copying bytes. |
| `Cut(start, size)` | Like `Slice`, but also removes the range from this stream. |
| `Move(start, destination, length)` | Relocate a range without copying bytes. |
| `Clone()` | New `PatchStream` sharing the same patch list. Independent position. |
| `SnapShot(useShared = true)` | Freeze the current state into a single-patch `PatchStream`. |

## Undo, redo, restore points

- `Undo()` / `Redo()` step through individual patches.
- `RestorePoint = true` marks the current patch. `RestorePointUndo()` / `RestorePointRedo()` skip between restore points — the first and last patch always behave as restore-point boundaries.
- `LatestStable` returns a `PatchStream` of the most recent restore point at or before the current patch. While a caller is mid-edit (writing several patches before marking a restore point), another thread can read `LatestStable` and see a consistent view of the last stable state.

## Events

- `OnChanged(sender, overwrittenPatches, affectedRegions)` — fires after every modification. Handlers may read the stream, but they should not assume `Position` is where you last left it; capture the position before calling the operation that fires the event.
- `OnRestorePointsChanged(sender)` — fires when a restore point is added or removed.

## Notes for embedders

- **Consumers that store `LatestStable` in a field** must be careful. `LatestStable` returns the most recent restore point *at the moment it is called*. If the current patch is not yet a restore point (e.g. inside an `OnChanged` handler fired from `Insert`), `LatestStable` may walk back to a previous snapshot. That snapshot is frozen — later restore points on the live stream will not update it. If you need to track the live stream, keep a separate reference to the original `PatchStream` and call `.LatestStable` on it fresh each time you need the current snapshot.
- **Sources must not be mutated externally** once handed to a `PatchStream`. The stream reads them on demand; mutating them from the outside is undefined behavior.

## License

MIT. See `LICENSE.txt`.
