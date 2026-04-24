// Tests for the higher-level Delete / Splice / Insert / Move / Slice / Cut
// operations on PatchStream. These operations are what consumers (e.g.
// SpawnDev.EBML) rely on for non-destructive editing.

using System.Text;
using NUnit.Framework;

namespace SpawnDev.PatchStreams.Tests;

[TestFixture]
public class PatchStreamOperationTests
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
    public void Delete_Middle_ShrinksStream()
    {
        using var ps = new PatchStream(Ascii("ABCDE"));
        ps.Delete(1, 2); // remove "BC"
        Assert.That(ReadAll(ps), Is.EqualTo("ADE"));
    }

    [Test]
    public void Delete_FromStart_RemovesPrefix()
    {
        using var ps = new PatchStream(Ascii("ABCDE"));
        ps.Delete(0, 2); // remove "AB"
        Assert.That(ReadAll(ps), Is.EqualTo("CDE"));
    }

    [Test]
    public void Delete_AtEnd_RemovesSuffix()
    {
        using var ps = new PatchStream(Ascii("ABCDE"));
        ps.Delete(3, 2); // remove "DE"
        Assert.That(ReadAll(ps), Is.EqualTo("ABC"));
    }

    [Test]
    public void Splice_ReplacesRange_WithDifferentLength()
    {
        using var ps = new PatchStream(Ascii("Hello, world"));
        ps.Splice(7, 5, (Stream)new MemoryStream(Ascii("Todd!")));
        Assert.That(ReadAll(ps), Is.EqualTo("Hello, Todd!"));
    }

    [Test]
    public void Splice_DeleteAll_AddNothing_LeavesEmpty()
    {
        using var ps = new PatchStream(Ascii("ABCD"));
        ps.Splice(0, 4, Array.Empty<Stream>());
        Assert.That(ps.Length, Is.EqualTo(0));
    }

    [Test]
    public void Insert_PrependsAtPosition()
    {
        using var ps = new PatchStream(Ascii("world"));
        ps.Position = 0;
        ps.Insert(Ascii("hello "));
        Assert.That(ReadAll(ps), Is.EqualTo("hello world"));
    }

    [Test]
    public void Slice_ReturnsSubView()
    {
        using var ps = new PatchStream(Ascii("0123456789"));
        using var view = ps.Slice(2, 4);
        Assert.That(ReadAll(view), Is.EqualTo("2345"));
    }

    [Test]
    public void Cut_RemovesAndReturnsSlice()
    {
        using var ps = new PatchStream(Ascii("0123456789"));
        using var cut = ps.Cut(2, 4);
        Assert.Multiple(() =>
        {
            Assert.That(ReadAll(cut), Is.EqualTo("2345"));
            Assert.That(ReadAll(ps), Is.EqualTo("016789"));
        });
    }

    [Test]
    public void Move_RelocatesBytes_ToEnd()
    {
        using var ps = new PatchStream(Ascii("ABCDE"));
        // Move "BC" (start=1, length=2) to destination 3, which is end-of-stream
        // after the cut ("ADE" -> length 3). Result: "ADEBC".
        ps.Move(1, 3, 2);
        Assert.That(ReadAll(ps), Is.EqualTo("ADEBC"));
    }

    [Test]
    public void Move_RelocatesBytes_ToFront()
    {
        using var ps = new PatchStream(Ascii("ABCDE"));
        // Move "DE" (start=3, length=2) to destination 0. Cut -> "ABC"; insert
        // "DE" at 0 -> "DEABC".
        ps.Move(3, 0, 2);
        Assert.That(ReadAll(ps), Is.EqualTo("DEABC"));
    }

    [Test]
    public void Move_OutOfRangeDestination_Throws()
    {
        using var ps = new PatchStream(Ascii("ABCDE"));
        // Destination 4 is past the post-cut length (5 - 2 = 3), must throw.
        Assert.Throws<ArgumentOutOfRangeException>(() => ps.Move(1, 4, 2));
    }

    [Test]
    public void Multiple_Deletes_AndInserts_ComposeCorrectly()
    {
        using var ps = new PatchStream(Ascii("Hello, world!"));
        ps.Splice(7, 5, (Stream)new MemoryStream(Ascii("Todd")));
        ps.Insert(Ascii("Dear "));
        // Splice at pos=7 replaces "world" with "Todd" -> "Hello, Todd!"
        // Insert at current position (just after the splice) prepends "Dear ".
        // So expected contains both "Todd" and "Dear".
        var result = ReadAll(ps);
        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Contain("Todd"));
            Assert.That(result, Does.Contain("Dear"));
        });
    }
}
