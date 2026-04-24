// SpawnDev.PatchStreams unit tests - basic read/write/position behaviour.
// Keeps every test short and mechanical. Fixture touches only documented
// public API (no reflection, no internals).

using System.Text;
using NUnit.Framework;

namespace SpawnDev.PatchStreams.Tests;

[TestFixture]
public class PatchStreamBasicTests
{
    private static byte[] Ascii(string s) => Encoding.ASCII.GetBytes(s);

    [Test]
    public void Construct_FromMemoryStream_LengthMatchesSource()
    {
        var src = new MemoryStream(Ascii("Hello, world!"));
        using var ps = new PatchStream(src);
        Assert.That(ps.Length, Is.EqualTo(13));
    }

    [Test]
    public void Construct_FromByteArray_LengthMatchesArray()
    {
        using var ps = new PatchStream(Ascii("Hello"));
        Assert.That(ps.Length, Is.EqualTo(5));
    }

    [Test]
    public void Position_StartsAtZero()
    {
        using var ps = new PatchStream(Ascii("abc"));
        Assert.That(ps.Position, Is.EqualTo(0));
    }

    [Test]
    public void Position_SeekToEnd_ReturnsLength()
    {
        using var ps = new PatchStream(Ascii("abc"));
        ps.Seek(0, SeekOrigin.End);
        Assert.That(ps.Position, Is.EqualTo(3));
    }

    [Test]
    public void Read_FullBuffer_ReturnsSourceBytes()
    {
        using var ps = new PatchStream(Ascii("Hello"));
        var buf = new byte[5];
        int got = ps.Read(buf, 0, 5);
        Assert.Multiple(() =>
        {
            Assert.That(got, Is.EqualTo(5));
            Assert.That(Encoding.ASCII.GetString(buf), Is.EqualTo("Hello"));
        });
    }

    [Test]
    public void Read_PastEnd_ReturnsZero()
    {
        using var ps = new PatchStream(Ascii("Hi"));
        ps.Position = 2;
        var buf = new byte[3];
        int got = ps.Read(buf, 0, 3);
        Assert.That(got, Is.EqualTo(0));
    }

    [Test]
    public void Read_MidStream_AdvancesPosition()
    {
        using var ps = new PatchStream(Ascii("ABCDE"));
        ps.Position = 1;
        var buf = new byte[2];
        ps.Read(buf, 0, 2);
        Assert.Multiple(() =>
        {
            Assert.That(Encoding.ASCII.GetString(buf), Is.EqualTo("BC"));
            Assert.That(ps.Position, Is.EqualTo(3));
        });
    }

    [Test]
    public void Write_Overwrite_ChangesBytes_PreservesLength()
    {
        using var ps = new PatchStream(Ascii("Hello"));
        ps.Position = 0;
        ps.Write(Ascii("HALLO"), 0, 5);
        ps.Position = 0;
        var buf = new byte[5];
        ps.Read(buf, 0, 5);
        Assert.Multiple(() =>
        {
            Assert.That(Encoding.ASCII.GetString(buf), Is.EqualTo("HALLO"));
            Assert.That(ps.Length, Is.EqualTo(5));
        });
    }

    [Test]
    public void Write_PastEnd_ExtendsLength()
    {
        using var ps = new PatchStream(Ascii("Hi"));
        ps.Position = 2;
        ps.Write(Ascii("!!!"), 0, 3);
        Assert.That(ps.Length, Is.EqualTo(5));
    }

    [Test]
    public void InsertWrites_True_GrowsStream()
    {
        using var ps = new PatchStream(Ascii("AB"));
        ps.InsertWrites = true;
        ps.Position = 1;
        ps.Write(Ascii("X"), 0, 1);
        ps.Position = 0;
        var buf = new byte[ps.Length];
        ps.Read(buf, 0, buf.Length);
        Assert.That(Encoding.ASCII.GetString(buf), Is.EqualTo("AXB"));
    }
}
