namespace SpawnDev.PatchStreams
{
    /// <summary>
    /// Similar to Lazy%lt;>, data is not actually accessed until first requested, and then the cached value is returned from then on.<br/>
    /// The difference here is that the Getter() is called again if the PatchId has changed in the specified stream.
    /// </summary>
    public abstract class VolatileData
    {
        /// <summary>
        /// Returns the Value as an object
        /// </summary>
        public abstract object? ValueData { get; }
        /// <summary>
        /// The source PatchStream
        /// </summary>
        public PatchStream PatchStream { get; protected set; }
        /// <summary>
        /// The patch id of PatchStream when the value was last updated, if ever
        /// </summary>
        public string? PatchId { get; protected set; }
        /// <summary>
        /// Optional data associated with this instance
        /// </summary>
        public object? Tag { get; set; }
        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="patchStream"></param>
        public VolatileData(PatchStream patchStream)
        {
            PatchStream = patchStream;
        }
    }
    /// <summary>
    /// Similar to Lazy%lt;>, data is not actually accessed until first requested, and then the cached value is returned from then on.<br/>
    /// The difference here is that the Getter() is called again if the PatchId has changed in the specified stream.
    /// </summary>
    public class VolatileData<TValue> : VolatileData
    {
        /// <summary>
        /// Returns the Value as an object
        /// </summary>
        public override object? ValueData => Value;
        TValue _Value = default!;
        /// <summary>
        /// Returns the Value, calling the Getter if PatchId does not match PatchStream.PatchId
        /// </summary>
        public TValue Value
        {
            get
            {
                if (PatchId != PatchStream.PatchId)
                {
                    PatchId = PatchStream.PatchId;
                    _Value = Getter(this);
                }
                return _Value;
            }
            set
            {
                Setter?.Invoke(this, value);
            }
        }
        private Func<VolatileData, TValue> Getter { get; set; }
        private Action<VolatileData, TValue>? Setter { get; set; }
        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="patchStream"></param>
        /// <param name="getter"></param>
        /// <param name="setter"></param>
        /// <param name="tag">Optional data to associate with this instance</param>
        public VolatileData(PatchStream patchStream, Func<VolatileData, TValue> getter, Action<VolatileData, TValue>? setter = null, object? tag = null) : base(patchStream)
        {
            Getter = getter;
            Setter = setter;
            Tag = tag;
        }
    }
}
