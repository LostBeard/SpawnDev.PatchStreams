// Tests for LatestStable and its interaction with edits + restore points.
// The `EBMLDocument` consumer uses LatestStable as its search-stream, so
// this behaviour is load-bearing for non-destructive editing.

using System.Text;
using NUnit.Framework;

namespace SpawnDev.PatchStreams.Tests;

[TestFixture]
public class LatestStableTests
{
    private static byte[] Ascii(string s) => Encoding.ASCII.GetBytes(s);

    private static string ReadAll(PatchStream ps)
    {
        ps.Position = 0;
        var buf = new byte[ps.Length];
        int read = 0;
        while (read < buf.Length)
        {
            int got = ps.Read(buf, read, buf.Length - read);
            if (got <= 0) break;
            read += got;
        }
        return Encoding.ASCII.GetString(buf, 0, read);
    }

    [Test]
    public void LatestStable_OnNewStream_RestorePointTrue_ReturnsCurrent()
    {
        using var ps = new PatchStream(new MemoryStream());
        ps.RestorePoint = true;
        // After marking the initial (empty) patch as a restore point, LatestStable
        // should return the CURRENT stream, not throw or return something stale.
        var stable = ps.LatestStable;
        Assert.That(stable, Is.Not.Null);
        Assert.That(stable.Length, Is.EqualTo(0));
    }

    [Test]
    public void LatestStable_AfterInsertWithoutRestorePoint_ReturnsPreviousStableSnapshot()
    {
        using var ps = new PatchStream(new MemoryStream());
        ps.RestorePoint = true;          // initial empty patch = stable
        ps.Position = 0;
        ps.Insert(Ascii("HELLO"));       // new patch, NOT a restore point
        var stable = ps.LatestStable;
        // Because the new patch isn't a restore point, LatestStable should fall
        // back to the previous restore point (the initial empty one).
        Assert.That(stable.Length, Is.EqualTo(0));
        // But the live stream has the inserted bytes:
        Assert.That(ReadAll(ps), Is.EqualTo("HELLO"));
    }

    [Test]
    public void LatestStable_AfterInsertThenSetRestorePoint_IncludesInsert()
    {
        // This is the exact pattern EBMLDocument needs after inserting an EBML
        // element: Insert, then mark the post-insert patch as a restore point so
        // downstream iterating code that uses LatestStable sees the new bytes.
        using var ps = new PatchStream(new MemoryStream());
        ps.RestorePoint = true;
        ps.Position = 0;
        ps.Insert(Ascii("HELLO"));
        ps.RestorePoint = true;          // mark post-insert patch as stable

        var stable = ps.LatestStable;
        Assert.That(stable.Length, Is.EqualTo(5),
            "LatestStable should see the just-inserted bytes once the post-insert patch is marked.");
        Assert.That(ReadAll(stable), Is.EqualTo("HELLO"));
    }

    [Test]
    public void LatestStable_MultipleEditsWithIntermediateRestorePoints_TracksMostRecent()
    {
        using var ps = new PatchStream(new MemoryStream());
        ps.RestorePoint = true;                // empty stable point
        ps.Position = 0;
        ps.Insert(Ascii("AB"));
        ps.RestorePoint = true;                // stable after first insert

        ps.Position = 2;
        ps.Insert(Ascii("CD"));                // not stable yet

        var stable = ps.LatestStable;
        // Should be the post-first-insert snapshot, not the current (AB+CD) state.
        Assert.That(ReadAll(stable), Is.EqualTo("AB"));

        ps.RestorePoint = true;                // now mark post-second-insert as stable too
        stable = ps.LatestStable;
        Assert.That(ReadAll(stable), Is.EqualTo("ABCD"));
    }
}
