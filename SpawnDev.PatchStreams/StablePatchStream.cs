namespace SpawnDev.PatchStreams
{
    public class StablePatchStream
    {
        public PatchStream Source { get; private set; }
        public PatchStream Stable
        {
            get
            {
                if (Source.RestorePoint && _Stable?.PatchId != Source.PatchId)
                {
                    _Stable = Source.SnapShot();
                }
                if (_Stable?.PatchId == Source.PatchId || _Stable == null)
                {
                    return Source;
                }
                else
                {
                    return _Stable;
                }
            }
        }
        private PatchStream? _Stable { get; set; }
        public bool SourceIsStable => Source.RestorePoint;
        public StablePatchStream(PatchStream patchStream)
        {
            Source = patchStream;
        }
    }
}