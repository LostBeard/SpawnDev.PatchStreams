// Tests for Undo / Redo / Snapshot / Clone / SetRestorePoint / RestorePointUndo.
// This is where the patch-based machinery is most likely to hide bugs.

using System.Text;
using NUnit.Framework;

namespace SpawnDev.PatchStreams.Tests;

[TestFixture]
public class PatchStreamHistoryTests
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
    public void Undo_AfterSingleWrite_RestoresOriginal()
    {
        using var ps = new PatchStream(Ascii("Hello"));
        ps.Position = 0;
        ps.Write(Ascii("World"), 0, 5);
        Assert.That(ReadAll(ps), Is.EqualTo("World"));
        Assert.That(ps.Undo(), Is.True);
        Assert.That(ReadAll(ps), Is.EqualTo("Hello"));
    }

    [Test]
    public void Redo_AfterUndo_ReappliesChange()
    {
        using var ps = new PatchStream(Ascii("Hello"));
        ps.Position = 0;
        ps.Write(Ascii("World"), 0, 5);
        ps.Undo();
        Assert.That(ps.Redo(), Is.True);
        Assert.That(ReadAll(ps), Is.EqualTo("World"));
    }

    [Test]
    public void Undo_TwiceFollowedByRedoTwice_RoundTrips()
    {
        using var ps = new PatchStream(Ascii("Hello"));
        ps.Position = 0;
        ps.Write(Ascii("HALLO"), 0, 5);
        ps.Position = 0;
        ps.Write(Ascii("GUTEN"), 0, 5);
        Assert.That(ReadAll(ps), Is.EqualTo("GUTEN"));
        ps.Undo();
        Assert.That(ReadAll(ps), Is.EqualTo("HALLO"));
        ps.Undo();
        Assert.That(ReadAll(ps), Is.EqualTo("Hello"));
        ps.Redo();
        Assert.That(ReadAll(ps), Is.EqualTo("HALLO"));
        ps.Redo();
        Assert.That(ReadAll(ps), Is.EqualTo("GUTEN"));
    }

    [Test]
    public void Undo_AfterInsert_RestoresOriginalLength()
    {
        using var ps = new PatchStream(Ascii("AB"));
        ps.Insert(Ascii("XYZ"));
        Assert.That(ps.Length, Is.GreaterThan(2));
        ps.Undo();
        Assert.That(ps.Length, Is.EqualTo(2));
        Assert.That(ReadAll(ps), Is.EqualTo("AB"));
    }

    [Test]
    public void Undo_AfterDelete_RestoresDeletedBytes()
    {
        using var ps = new PatchStream(Ascii("Hello"));
        ps.Delete(1, 3); // remove "ell" -> "Ho"
        Assert.That(ReadAll(ps), Is.EqualTo("Ho"));
        ps.Undo();
        Assert.That(ReadAll(ps), Is.EqualTo("Hello"));
    }

    [Test]
    public void Snapshot_CapturesFrozenState_UnaffectedByLaterEdits()
    {
        using var ps = new PatchStream(Ascii("Hello"));
        using var snap = ps.SnapShot();
        ps.Position = 0;
        ps.Write(Ascii("World"), 0, 5);
        Assert.Multiple(() =>
        {
            Assert.That(ReadAll(ps), Is.EqualTo("World"));
            Assert.That(ReadAll(snap), Is.EqualTo("Hello"));
        });
    }

    [Test]
    public void Clone_SharesSourceButIndependentPatches()
    {
        using var ps = new PatchStream(Ascii("Hello"));
        using var clone = ps.Clone();
        ps.Position = 0;
        ps.Write(Ascii("World"), 0, 5);
        // Clone's state depends on implementation; verify it didn't see the new write.
        // Per the CLAUDE.md: "Clone (shares patches by reference)" - meaning initial patch
        // list is shared. After divergent writes, clone stays at its own head.
        Assert.That(ReadAll(clone), Is.EqualTo("Hello"));
    }

    [Test]
    public void RestorePoint_Undo_RevertsToCheckpoint()
    {
        using var ps = new PatchStream(Ascii("Hello"));
        ps.Position = 0;
        ps.Write(Ascii("HALLO"), 0, 5);
        // Mark current patch as a restore point via the property setter.
        ps.RestorePoint = true;
        ps.Position = 0;
        ps.Write(Ascii("GUTEN"), 0, 5);
        Assert.That(ReadAll(ps), Is.EqualTo("GUTEN"));
        bool reverted = ps.RestorePointUndo();
        Assert.That(reverted, Is.True);
        Assert.That(ReadAll(ps), Is.EqualTo("HALLO"));
    }

    [Test]
    public void Undo_OnUnchangedStream_ReturnsFalse()
    {
        using var ps = new PatchStream(Ascii("Hello"));
        // No edits have been made; Undo should have nothing to do.
        Assert.That(ps.Undo(), Is.False);
    }

    [Test]
    public void Redo_OnStreamWithNoUndoneChanges_ReturnsFalse()
    {
        using var ps = new PatchStream(Ascii("Hello"));
        ps.Position = 0;
        ps.Write(Ascii("World"), 0, 5);
        // No undo performed; Redo should return false.
        Assert.That(ps.Redo(), Is.False);
    }

    [Test]
    public void NewEdit_AfterUndo_DiscardsRedoHistory()
    {
        using var ps = new PatchStream(Ascii("Hello"));
        ps.Position = 0;
        ps.Write(Ascii("World"), 0, 5);
        ps.Undo();
        // New edit while Redo stack has an entry should discard Redo history.
        ps.Position = 0;
        ps.Write(Ascii("EARTH"), 0, 5);
        Assert.That(ReadAll(ps), Is.EqualTo("EARTH"));
        // After the new write, there is nothing to Redo.
        Assert.That(ps.Redo(), Is.False);
    }
}
