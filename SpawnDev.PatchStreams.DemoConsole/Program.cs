using SpawnDev.PatchStreams;
using System.Reflection;

// Create a new PatchStream with or without source data.
// Source data and data added to PatchStream should not be modified once it is added
//var patchStream = new PatchStream(/* IEnumerable<Stream> stream(s), long offset?, long size? */);
//var memStream = new MemoryStream();
//memStream.WriteByte((byte)'Q');
//memStream.Position = 0;
var patchStream = new PatchStream();

patchStream.OnChanged += PatchStream_OnChanged;

void PatchStream_OnChanged(PatchStream sender, IEnumerable<Patch> overwrittenPatches, IEnumerable<ByteRange> affectedRegions)
{
    var i = 0;
    Console.WriteLine($"Regions changed: {affectedRegions.Count()}");
    foreach (var range in affectedRegions)
    {
        Console.WriteLine($"- {i} start: {range.Start} size: {range.Size}");
    }
    Console.WriteLine(string.Join(" ", sender.ToArray(true)));
}

patchStream.Insert("defabc");
patchStream.Move(0, 3, 3);
Console.WriteLine(string.Join(" ", patchStream.ToString(true)));


patchStream.Position = 0;
patchStream.Insert("123", 3);
patchStream.Position = 6;
patchStream.Insert("xyz");
patchStream.Position = 0;
patchStream.Insert("1");
patchStream.Position = 0;
Console.WriteLine(string.Join(" ", patchStream.ToArray(true)));



patchStream.Position = 0; patchStream.Write("0EBML");
patchStream.Write("/DocType");
patchStream.Position = 0;
patchStream.Insert("1", 1);
Console.WriteLine(patchStream.ToString(true));


patchStream.Write("world!");
// patchStream data is now "world!"

// prepend "Hello "
patchStream.InsertWrites = true;
patchStream.Position = 0;
patchStream.Write("Hello ");
patchStream.Flush();

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

patchStream.InsertWrites = false;
patchStream.Position = 0;
patchStream.Write("Presen1ing: ");
patchStream.Position = 1;
patchStream.Write("resenting: ");
patchStream.Position = 0;
patchStream.InsertWrites = true;

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

patchStream.InsertWrites = false;
patchStream.Position = 6;
patchStream.Write("Blazor");
Console.WriteLine(patchStream.ToString(true));


foreach (var patch in patchStream.Patches)
{
    Console.WriteLine($"{patch.Size} {patch.ChangeOffset} {patch.DeletedByteCount} {patch.InsertedByteCount}");
}
var nmttt = true;