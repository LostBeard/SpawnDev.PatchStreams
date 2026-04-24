// Audit probes - targeted tests for suspicious areas found during a code
// review of PatchStream.cs. Each test names the concern it is probing so
// failures map directly to a specific design/implementation issue.

using System.Text;
using NUnit.Framework;

namespace SpawnDev.PatchStreams.Tests;

[TestFixture]
public class PatchStreamAuditTests
{
    private static byte[] Ascii(string s) => Encoding.ASCII.GetBytes(s);

    private static string ReadAll(PatchStream ps)
    {
        ps.Position = 0;
        var buf = new byte[ps.Length];
        int read = 0;
        while (read < buf.Length)
        {
            int got = ps.Read(buf, 0 + read, buf.Length - read);
            if (got <= 0) break;
            read += got;
        }
        return Encoding.ASCII.GetString(buf, 0, read);
    }

    // ---------------------------------------------------------------
    // Multi-source read probes.
    //
    // PatchStream.Read walks a list of Sources streams. The current
    // implementation has two suspicious expressions:
    //   while (source.Length < currentOffset)          // (A) < vs <=
    //   if (sourceIndex >= source.Length - 1) return 0 // (B) uses byte length, not source count
    // If either is wrong, reads that span source boundaries - or land
    // exactly on a boundary - will either hang, return wrong data, or
    // return 0 prematurely.
    // ---------------------------------------------------------------

    [Test]
    public void MultiSource_Read_SpansBoundary()
    {
        // Three sources glued together: "AAA" + "BBB" + "CCC".
        var sources = new Stream[]
        {
            new MemoryStream(Ascii("AAA")),
            new MemoryStream(Ascii("BBB")),
            new MemoryStream(Ascii("CCC")),
        };
        using var ps = new PatchStream(sources);
        Assert.That(ps.Length, Is.EqualTo(9), "combined length must be 9");
        Assert.That(ReadAll(ps), Is.EqualTo("AAABBBCCC"));
    }

    [Test]
    public void MultiSource_Read_StartingExactlyOnBoundary()
    {
        var sources = new Stream[]
        {
            new MemoryStream(Ascii("AAA")),
            new MemoryStream(Ascii("BBB")),
        };
        using var ps = new PatchStream(sources);
        ps.Position = 3;            // Exactly on the boundary between sources.
        var buf = new byte[3];
        int got = ps.Read(buf, 0, 3);
        Assert.Multiple(() =>
        {
            Assert.That(got, Is.EqualTo(3));
            Assert.That(Encoding.ASCII.GetString(buf), Is.EqualTo("BBB"));
        });
    }

    [Test]
    public void MultiSource_Read_ReadCrossingBoundary()
    {
        var sources = new Stream[]
        {
            new MemoryStream(Ascii("AAA")),
            new MemoryStream(Ascii("BBB")),
        };
        using var ps = new PatchStream(sources);
        ps.Position = 2;            // 1 byte left in first source, then cross.
        var buf = new byte[3];
        int got = ps.Read(buf, 0, 3);
        Assert.Multiple(() =>
        {
            Assert.That(got, Is.EqualTo(3));
            Assert.That(Encoding.ASCII.GetString(buf), Is.EqualTo("ABB"));
        });
    }

    [Test]
    public void ManySources_TinyChunks_ReadFullStream()
    {
        // Stress the multi-source walk with 20 one-byte sources. If the
        // boundary logic is wrong at all, this breaks somewhere in the middle.
        var sources = new List<Stream>();
        for (int i = 0; i < 20; i++)
            sources.Add(new MemoryStream(new[] { (byte)('A' + i) }));
        using var ps = new PatchStream(sources);
        Assert.That(ps.Length, Is.EqualTo(20));
        var buf = new byte[20];
        ps.Position = 0;
        int got = ps.Read(buf, 0, 20);
        Assert.Multiple(() =>
        {
            Assert.That(got, Is.EqualTo(20));
            Assert.That(Encoding.ASCII.GetString(buf),
                Is.EqualTo("ABCDEFGHIJKLMNOPQRST"));
        });
    }

    // ---------------------------------------------------------------
    // PatchIndex getter clamp probe.
    //
    // The getter clamps to [0, _Patches.Count], but the valid index range
    // is [0, _Patches.Count - 1]. Accessing `Patch` after a clamped max
    // would throw IndexOutOfRangeException.
    // ---------------------------------------------------------------

    [Test]
    public void PatchIndex_AfterOperations_AlwaysPointsToValidPatch()
    {
        using var ps = new PatchStream(Ascii("A"));
        ps.Insert(Ascii("B"));
        ps.Insert(Ascii("C"));
        // Three patches: initial + 2 inserts. PatchIndex = 2.
        Assert.That(ps.PatchIndex, Is.EqualTo(2));
        Assert.DoesNotThrow(() => _ = ps.PatchId);
    }

    // ---------------------------------------------------------------
    // Undo/Redo round-trip integrity probes.
    // ---------------------------------------------------------------

    [Test]
    public void Undo_Redo_RoundTrips_Content()
    {
        using var ps = new PatchStream(Ascii("AAA"));
        ps.Position = 3;
        ps.Insert(Ascii("BBB"));
        Assert.That(ReadAll(ps), Is.EqualTo("AAABBB"));
        ps.Undo();
        Assert.That(ReadAll(ps), Is.EqualTo("AAA"));
        ps.Redo();
        Assert.That(ReadAll(ps), Is.EqualTo("AAABBB"));
    }

    [Test]
    public void ModifyAfterUndo_DiscardsFuturePatches()
    {
        using var ps = new PatchStream(Ascii("AAA"));
        ps.Position = 3;
        ps.Insert(Ascii("BBB"));   // patch 1: AAABBB
        ps.Insert(Ascii("CCC"));   // patch 2: AAABBBCCC
        ps.Undo();                  // back to AAABBB
        Assert.That(ps.CanRedo, Is.True, "redo available before new edit");
        ps.Position = ps.Length;
        ps.Insert(Ascii("ZZZ"));   // should overwrite the CCC future patch
        Assert.Multiple(() =>
        {
            Assert.That(ReadAll(ps), Is.EqualTo("AAABBBZZZ"));
            Assert.That(ps.CanRedo, Is.False, "redo must be gone after divergent edit");
        });
    }

    // ---------------------------------------------------------------
    // LatestStable probes.
    // ---------------------------------------------------------------

    [Test]
    public void LatestStable_OnFreshStream_IsThis()
    {
        using var ps = new PatchStream(Ascii("A"));
        // First patch is always a restore point, so LatestStable == this.
        Assert.That(ps.LatestStable.PatchId, Is.EqualTo(ps.PatchId));
    }

    [Test]
    public void LatestStable_AfterNonRestoreEdit_ReflectsPriorRestore()
    {
        using var ps = new PatchStream(Ascii("A"));
        // Mark initial patch as restore point (it already is, but be explicit).
        ps.RestorePoint = true;
        var initialId = ps.PatchId;
        ps.Position = ps.Length;
        ps.Insert(Ascii("B"));      // patch 1, NOT a restore point
        Assert.That(ps.PatchId, Is.Not.EqualTo(initialId));
        // LatestStable should walk BACK to the prior restore point (patch 0).
        Assert.That(ps.LatestStable.PatchId, Is.EqualTo(initialId));
        Assert.That(ReadAll(ps.LatestStable), Is.EqualTo("A"));
    }

    [Test]
    public void LatestStable_WhenCurrentIsRestorePoint_IsThis()
    {
        using var ps = new PatchStream(Ascii("A"));
        ps.Position = ps.Length;
        ps.Insert(Ascii("B"));
        ps.RestorePoint = true;     // mark current as restore point
        Assert.Multiple(() =>
        {
            Assert.That(ps.LatestStable.PatchId, Is.EqualTo(ps.PatchId));
            Assert.That(ReadAll(ps.LatestStable), Is.EqualTo("AB"));
        });
    }

    // ---------------------------------------------------------------
    // Clone / SnapShot independence probes.
    // ---------------------------------------------------------------

    [Test]
    public void Clone_SharesPatchesButHasIndependentPosition()
    {
        using var ps = new PatchStream(Ascii("ABCDE"));
        ps.Position = 2;
        using var clone = ps.Clone();
        clone.Position = 0;
        Assert.Multiple(() =>
        {
            Assert.That(ps.Position, Is.EqualTo(2), "original position unchanged");
            Assert.That(clone.Position, Is.EqualTo(0));
            Assert.That(ReadAll(clone), Is.EqualTo("ABCDE"));
        });
    }

    [Test]
    public void SnapShot_DoesNotChange_WhenOriginalMutates()
    {
        using var ps = new PatchStream(Ascii("Hi"));
        using var snap = ps.SnapShot();
        var snapBefore = ReadAll(snap);
        ps.Position = ps.Length;
        ps.Insert(Ascii("!!!"));
        var snapAfter = ReadAll(snap);
        Assert.Multiple(() =>
        {
            Assert.That(snapBefore, Is.EqualTo("Hi"));
            Assert.That(snapAfter, Is.EqualTo("Hi"), "snapshot must be frozen");
            Assert.That(ReadAll(ps), Is.EqualTo("Hi!!!"), "original advanced");
        });
    }

    // ---------------------------------------------------------------
    // OnChanged event ordering probes.
    //
    // Consumers (SpawnDev.EBML) rely on OnChanged firing AFTER the new
    // patch is active. If the event fired before PatchIndex advanced,
    // handlers would see stale content.
    // ---------------------------------------------------------------

    [Test]
    public void OnChanged_FiresAfterPatchBecomesActive()
    {
        using var ps = new PatchStream(Ascii("A"));
        var initialPatchId = ps.PatchId;
        string? observedPatchIdDuringEvent = null;
        ps.OnChanged += (sender, overwritten, regions) =>
        {
            observedPatchIdDuringEvent = sender.PatchId;
        };
        ps.Position = ps.Length;
        ps.Insert(Ascii("B"));
        Assert.Multiple(() =>
        {
            Assert.That(observedPatchIdDuringEvent, Is.Not.Null);
            Assert.That(observedPatchIdDuringEvent, Is.Not.EqualTo(initialPatchId));
            Assert.That(observedPatchIdDuringEvent, Is.EqualTo(ps.PatchId));
        });
    }

    [Test]
    public void OnChanged_StreamStateVisibleInHandler_HasInsertedBytes()
    {
        using var ps = new PatchStream(Ascii("A"));
        string? observed = null;
        ps.OnChanged += (sender, _, _) =>
        {
            sender.Position = 0;
            var buf = new byte[sender.Length];
            int read = 0;
            while (read < buf.Length)
            {
                int got = sender.Read(buf, read, buf.Length - read);
                if (got <= 0) break;
                read += got;
            }
            observed = Encoding.ASCII.GetString(buf, 0, read);
        };
        ps.Position = ps.Length;
        ps.Insert(Ascii("B"));
        Assert.That(observed, Is.EqualTo("AB"),
            "handler must see the post-insert stream state");
    }

    // ---------------------------------------------------------------
    // Write-at-EOF in overwrite mode: documented behaviour?
    //
    // When InsertWrites == false (default) and Position == Length, the
    // Splice called by Write has a zero-byte delete range. Effectively
    // this extends the stream. This test just pins the observed behaviour
    // so future refactors don't regress it silently.
    // ---------------------------------------------------------------

    [Test]
    public void Write_OverwriteMode_AtEnd_ExtendsStream()
    {
        using var ps = new PatchStream(Ascii("AB"));
        ps.Position = ps.Length;
        ps.Write(Ascii("CD"), 0, 2);
        Assert.Multiple(() =>
        {
            Assert.That(ps.Length, Is.EqualTo(4));
            Assert.That(ReadAll(ps), Is.EqualTo("ABCD"));
        });
    }

    // ---------------------------------------------------------------
    // Slice creates a read view that itself is backed by a PatchStream.
    // Writing to a slice MUST NOT modify the original.
    // ---------------------------------------------------------------

    [Test]
    public void Slice_Write_DoesNotModifyOriginal()
    {
        using var ps = new PatchStream(Ascii("ABCDEF"));
        using var slice = ps.Slice(1, 3); // "BCD"
        Assert.That(ReadAll(slice), Is.EqualTo("BCD"));
        slice.Position = 0;
        slice.Write(Ascii("xyz"), 0, 3);
        Assert.Multiple(() =>
        {
            Assert.That(ReadAll(slice), Is.EqualTo("xyz"));
            Assert.That(ReadAll(ps), Is.EqualTo("ABCDEF"),
                "writing to slice must not modify parent");
        });
    }

    // ---------------------------------------------------------------
    // Splice with MULTIPLE insert streams - verifies the params Stream[]
    // overload splices them together in the right order.
    // ---------------------------------------------------------------

    [Test]
    public void Splice_MultipleInsertStreams_AreGluedInOrder()
    {
        using var ps = new PatchStream(Ascii("ABCD"));
        ps.Splice(2, 0,
            new MemoryStream(Ascii("X")),
            new MemoryStream(Ascii("Y")),
            new MemoryStream(Ascii("Z")));
        Assert.That(ReadAll(ps), Is.EqualTo("ABXYZCD"));
    }

    // ---------------------------------------------------------------
    // Deep patch chain - stresses the internal list of patches.
    // 100 inserts simulate building up an EBML document header by header.
    // ---------------------------------------------------------------

    [Test]
    public void ManyInserts_ReadReflectsFullContent()
    {
        using var ps = new PatchStream(Array.Empty<byte>());
        var sb = new StringBuilder();
        for (int i = 0; i < 100; i++)
        {
            ps.Position = ps.Length;
            ps.Insert(Ascii(i.ToString("D3")));
            sb.Append(i.ToString("D3"));
        }
        Assert.That(ReadAll(ps), Is.EqualTo(sb.ToString()));
    }

    // ---------------------------------------------------------------
    // After many inserts, LatestStable on a stream that never had a
    // RestorePoint set returns the very first (empty) patch. This pins
    // the documented semantics - you MUST set RestorePoint before relying
    // on LatestStable reflecting your edits.
    // ---------------------------------------------------------------

    // ---------------------------------------------------------------
    // Read-past-first-source when the first source is tiny.
    //
    // PatchStream.Read's source-walk loop contains:
    //   if (sourceIndex >= source.Length - 1) return 0;
    // That comparison is between an INDEX and the CURRENT SOURCE'S BYTE
    // LENGTH. If Sources[0] is only 1 byte and we want to read past it,
    // the loop aborts prematurely and Read returns 0. The comparison
    // should be against Sources.Count - 1 (count of sources, not bytes).
    //
    // The cheapest reproducible case: first source is a single byte,
    // second source has real data, we try to read from the second.
    // ---------------------------------------------------------------

    [Test]
    public void Read_FirstSourceOneByte_ReadsFromSecondSource()
    {
        var sources = new Stream[]
        {
            new MemoryStream(Ascii("A")),
            new MemoryStream(Ascii("BCDEF")),
        };
        using var ps = new PatchStream(sources);
        Assert.That(ps.Length, Is.EqualTo(6), "combined length must be 6");
        ps.Position = 1;            // exactly on the boundary
        var buf = new byte[5];
        int got = ps.Read(buf, 0, 5);
        Assert.Multiple(() =>
        {
            Assert.That(got, Is.EqualTo(5));
            Assert.That(Encoding.ASCII.GetString(buf), Is.EqualTo("BCDEF"));
        });
    }

    [Test]
    public void Read_FirstSourceOneByte_Position2_ReadsRemainder()
    {
        var sources = new Stream[]
        {
            new MemoryStream(Ascii("A")),
            new MemoryStream(Ascii("BCDEF")),
        };
        using var ps = new PatchStream(sources);
        ps.Position = 2;
        var buf = new byte[4];
        int got = ps.Read(buf, 0, 4);
        Assert.Multiple(() =>
        {
            Assert.That(got, Is.EqualTo(4));
            Assert.That(Encoding.ASCII.GetString(buf), Is.EqualTo("CDEF"));
        });
    }

    [Test]
    public void LatestStable_WithoutExplicitRestorePoint_ReturnsInitial()
    {
        using var ps = new PatchStream(Ascii("seed"));
        ps.Position = ps.Length;
        ps.Insert(Ascii("more"));
        // No RestorePoint set on the new patch. LatestStable should fall
        // back to the first patch (which is always a restore point).
        Assert.That(ReadAll(ps.LatestStable), Is.EqualTo("seed"),
            "no explicit RestorePoint -> LatestStable is the initial patch");
        Assert.That(ReadAll(ps), Is.EqualTo("seedmore"),
            "live stream still shows the insert");
    }

    // ---------------------------------------------------------------
    // Async read must match sync read byte-for-byte on the same content,
    // including the multi-source boundary fix applied to both.
    // ---------------------------------------------------------------

    [Test]
    public async Task ReadAsync_MatchesRead_AcrossSourceBoundary()
    {
        var sources = new Stream[]
        {
            new MemoryStream(Ascii("A")),
            new MemoryStream(Ascii("BCDEF")),
        };
        using var ps = new PatchStream(sources);
        ps.Position = 2;
        var buf = new byte[4];
        int got = await ps.ReadAsync(buf, 0, 4);
        Assert.Multiple(() =>
        {
            Assert.That(got, Is.EqualTo(4));
            Assert.That(Encoding.ASCII.GetString(buf), Is.EqualTo("CDEF"));
        });
    }

    // ---------------------------------------------------------------
    // Splice with deleteCount > remaining - should clamp to remaining.
    // ---------------------------------------------------------------

    [Test]
    public void Splice_DeleteCountPastEnd_Clamps()
    {
        using var ps = new PatchStream(Ascii("ABCDE"));
        // From position 3 there are only 2 bytes; request deleting 100.
        ps.Splice(3, 100, Array.Empty<Stream>());
        Assert.Multiple(() =>
        {
            Assert.That(ps.Length, Is.EqualTo(3));
            Assert.That(ReadAll(ps), Is.EqualTo("ABC"));
        });
    }

    [Test]
    public void Splice_DeleteCountNegative_DeletesToEnd()
    {
        using var ps = new PatchStream(Ascii("ABCDE"));
        ps.Splice(2, -1, Array.Empty<Stream>());
        Assert.Multiple(() =>
        {
            Assert.That(ps.Length, Is.EqualTo(2));
            Assert.That(ReadAll(ps), Is.EqualTo("AB"));
        });
    }

    // ---------------------------------------------------------------
    // Delete that reduces content below current Position should clamp
    // Position into range (either Length or 0), not leave Position stale.
    // ---------------------------------------------------------------

    [Test]
    public void Delete_PastCurrentPosition_PositionClampsToLength()
    {
        using var ps = new PatchStream(Ascii("ABCDE"));
        ps.Position = 4;
        ps.Delete(2, 3);  // remove "CDE" -> "AB"
        // The underlying setter throws if Position > Length, so Position
        // must have been clamped by the read path. The getter clamps to
        // [0, Length] so observed value is Length.
        Assert.Multiple(() =>
        {
            Assert.That(ps.Length, Is.EqualTo(2));
            Assert.That(ps.Position, Is.LessThanOrEqualTo(ps.Length));
        });
    }

    // ---------------------------------------------------------------
    // Multi-source with zero-length source in the middle.
    //
    // PatchStream currently filters empty sources out during construction.
    // Pin that behaviour via test so it can't regress silently.
    // ---------------------------------------------------------------

    [Test]
    public void MultiSource_EmptySourceInMiddle_IsFilteredTransparently()
    {
        var sources = new Stream[]
        {
            new MemoryStream(Ascii("AB")),
            new MemoryStream(Array.Empty<byte>()),
            new MemoryStream(Ascii("CD")),
        };
        using var ps = new PatchStream(sources);
        Assert.That(ps.Length, Is.EqualTo(4));
        Assert.That(ReadAll(ps), Is.EqualTo("ABCD"));
    }

    // ---------------------------------------------------------------
    // Cross-boundary read where the SECOND source is hit mid-read.
    // ---------------------------------------------------------------

    [Test]
    public void Read_CrossingBoundary_InOneCall_ReturnsJoinedBytes()
    {
        var sources = new Stream[]
        {
            new MemoryStream(Ascii("AB")),
            new MemoryStream(Ascii("CDEF")),
        };
        using var ps = new PatchStream(sources);
        ps.Position = 1;           // start in first source
        var buf = new byte[5];     // request spans boundary
        int got = ps.Read(buf, 0, 5);
        Assert.Multiple(() =>
        {
            Assert.That(got, Is.EqualTo(5));
            Assert.That(Encoding.ASCII.GetString(buf), Is.EqualTo("BCDEF"));
        });
    }

    // ---------------------------------------------------------------
    // Insert preserves the intended post-insert cursor when a handler
    // does nothing (the common case). Regression companion to the
    // OnChanged mutation fix.
    // ---------------------------------------------------------------

    [Test]
    public void Insert_LeavesPositionAfterInsertedBytes()
    {
        using var ps = new PatchStream(Ascii("AABB"));
        ps.Position = 2;
        ps.Insert(Ascii("XYZ"));
        // Inserted "XYZ" at position 2 -> "AAXYZBB". Cursor now at 5.
        Assert.Multiple(() =>
        {
            Assert.That(ps.Position, Is.EqualTo(5));
            Assert.That(ReadAll(ps), Is.EqualTo("AAXYZBB"));
        });
    }

    // ---------------------------------------------------------------
    // EBMLDocument.CreateDocument path reproduction.
    //
    // EBMLDocument's code does:
    //   Info.Stream = new PatchStream(new MemoryStream());   // empty seed
    //   Stream.RestorePoint = true;                           // mark patch 0 as RP
    //   Stream.Position = 0;
    //   Stream.Insert(headerBytes);                           // 5 bytes
    //   Stream.RestorePoint = true;                           // mark new patch as RP
    //
    // After this sequence, LatestStable should contain those 5 bytes and
    // a fresh Read(0..5) must return them exactly. This is the path that
    // EBML's CreateDocument test fails on.
    // ---------------------------------------------------------------

    [Test]
    public void EBMLDocumentPattern_EmptySeed_ThenInsertHeader_ReadsBackBytes()
    {
        var ps = new PatchStream(new MemoryStream());
        ps.RestorePoint = true;
        Assert.That(ps.Length, Is.EqualTo(0), "seed stream is empty");

        var headerBytes = new byte[] { 0x1A, 0x45, 0xDF, 0xA3, 0x80 };
        ps.Position = 0;
        ps.Insert(new MemoryStream(headerBytes));
        ps.RestorePoint = true;

        Assert.That(ps.Length, Is.EqualTo(5));
        // LatestStable must show the post-insert state.
        Assert.That(ps.LatestStable.Length, Is.EqualTo(5),
            "LatestStable must reflect the just-committed restore point");

        var stable = ps.LatestStable;
        stable.Position = 0;
        var buf = new byte[5];
        int read = 0;
        while (read < 5)
        {
            int got = stable.Read(buf, read, 5 - read);
            if (got <= 0) break;
            read += got;
        }
        Assert.Multiple(() =>
        {
            Assert.That(read, Is.EqualTo(5));
            Assert.That(buf, Is.EqualTo(headerBytes));
        });
    }

    // Same pattern but reading through the LIVE stream (not LatestStable).
    [Test]
    public void EBMLDocumentPattern_LiveStream_ReadsInsertedHeader()
    {
        var ps = new PatchStream(new MemoryStream());
        ps.RestorePoint = true;
        var headerBytes = new byte[] { 0x1A, 0x45, 0xDF, 0xA3, 0x80 };
        ps.Position = 0;
        ps.Insert(new MemoryStream(headerBytes));

        ps.Position = 0;
        var buf = new byte[5];
        int read = 0;
        while (read < 5)
        {
            int got = ps.Read(buf, read, 5 - read);
            if (got <= 0) break;
            read += got;
        }
        Assert.Multiple(() =>
        {
            Assert.That(read, Is.EqualTo(5));
            Assert.That(buf, Is.EqualTo(headerBytes));
        });
    }
}
