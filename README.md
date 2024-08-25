# SpawnDev.PatchStreams

[![NuGet version](https://badge.fury.io/nu/SpawnDev.PatchStreams.svg)](https://www.nuget.org/packages/SpawnDev.PatchStreams)

## PatchStream : Stream

- PatchStream inherits from [Stream](https://learn.microsoft.com/en-us/dotnet/api/system.io.stream?view=net-8.0) making it easy to use with countless existing libraries that can work with Streams.
- It is a readable, writable stream that, when modified, does not modify any source data or data added to it, but instead creates patches to represent the data changes.
- Supports data deletion, insertion, overwriting, undo/redo, restore points, partial data views, multi-stream read-only sources, and multiple stream insertion.
- Restore points can be set at any point and restored easily to make undo/redo easier.
- All modifications to this stream are saved in patches which can be undone and redone as fast as changing a single integer value
- Low memory usage and blazing fast modifications.
- IMPORTANT - Once data has been added to a PatchStream it should not be modified in any way. Modify the PatchStream itself.

