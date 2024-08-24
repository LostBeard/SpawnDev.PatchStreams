# SpawnDev.PatchStreams

[![NuGet version](https://badge.fury.io/nu/SpawnDev.PatchStreams.svg)](https://www.nuget.org/packages/SpawnDev.PatchStreams)

## PatchStream

- A PatchStream is a writable stream that, when modified, does not modify any source data or data added to it, but instead creates patches to represent the data changes.
- This is useful if you want to temporarily modify a read a large read-only stream without making a complete copy of it
- As far as the user of the PatchStream Stream is concerned the stream is readable and writable even though any added data is never modified
- All modifications to this stream are saved in patches which can be undone and redone as fast as changing a single integer value
- Restore points can be set at any point and restored easily to make undo/redo easier.
- Supports data deletion, insertion, overwriting, undo/redo, restore points, partial data views, multi-stream read-only sources, and multiple stream insertion.
- Low memory usage and blazing fast modifications.
- IMPORTANT - Once data has been added to a PatchStream it should not be modified in any way. Modify the PatchStream itself.

