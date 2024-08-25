using SpawnDev.PatchStreams;

// Create a new PatchStream with or without source data.
// Source data and data added to PatchStream should not be modified once it is added
var patchStream = new PatchStream(/* IEnumerable<Stream> stream(s), long offset?, long size? */);
patchStream.Write("world!");

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
