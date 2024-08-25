# SpawnDev.PatchStreams

[![NuGet version](https://badge.fury.io/nu/SpawnDev.PatchStreams.svg)](https://www.nuget.org/packages/SpawnDev.PatchStreams)

## PatchStream : Stream

- PatchStream inherits from [Stream](https://learn.microsoft.com/en-us/dotnet/api/system.io.stream?view=net-8.0) making it easy to use with countless existing libraries that can work with Streams.
- It is a readable, writable stream that, when modified, does not modify any source data or data added to it, but instead creates patches to represent the data changes.
- Supports data deletion, insertion, overwriting, slicing, splicing, undo/redo, restore points, partial data views, multi-stream read-only sources, and multiple stream insertion.
- All modifications to a PatchStream are saved in patches which can be undone and redone as fast as changing a single integer value
- Restore points can be set at any point and restored easily to make undo/redo easier.
- Low memory usage and blazing fast modifications.
- IMPORTANT - Once data has been added to a PatchStream it should not be modified in any way. Modify the PatchStream itself.


The below code is a basic demonstration of PatchStream reading writing, inserting, deleting, restore point usage, and an undo.
```cs
using SpawnDev.PatchStreams;

// Create a new PatchStream with or without source data.
// Source data and data added to PatchStream should not be modified once it is added
var patchStream = new PatchStream(new MemoryStream());
patchStream.Write("world!");
// patchStream data is now "world!"

// prepend "Hello "
patchStream.InsertWrites = true;
patchStream.Position = 0;
patchStream.Write("Hello ");

// patchStream data is now "Hello world!"
Console.WriteLine(patchStream.ToString(true));

// set restore point for the current patch. we can revert back later
patchStream.RestorePoint = true;

// overwrite "world!" with "DotNet!"
patchStream.InsertWrites = false;
patchStream.Position = 6;
patchStream.Write("DotNet!");

// patchStream data is now "Hello DotNet!"
Console.WriteLine(patchStream.ToString(true));

// prepend "Presenting: "
patchStream.InsertWrites = true;
patchStream.Position = 0;
patchStream.Write("Presenting: ");

// delete data
patchStream.Delete();

// patchStream data is now "" and patchStream.Length == 0
Console.WriteLine("Empty ->" + patchStream.ToString(true));

// undo the last modification, which was a Delete()
patchStream.Undo();

// patchStream data is now "Presenting: Hello DotNet!"
Console.WriteLine(patchStream.ToString(true));

// go to the most recent restore point
patchStream.RestorePointUndo();

// patchStream data is now "Hello world!"
Console.WriteLine(patchStream.ToString(true));

// Flush all changes to the original source stream (for single, writable source streams only)
patchStream.Flush();

```