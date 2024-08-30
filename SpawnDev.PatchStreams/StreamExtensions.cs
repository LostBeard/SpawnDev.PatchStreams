namespace SpawnDev.PatchStreams
{
    /// <summary>
    /// Extension methods for Stream
    /// </summary>
    public static class StreamExtensions
    {
        /// <summary>
        /// Copy the specified number of bytes from this stream to the destination stream
        /// </summary>
        /// <param name="_this"></param>
        /// <param name="destination"></param>
        /// <param name="bytes"></param>
        public static void CopyTo(this Stream _this, Stream destination, int bytes)
        {
            byte[] buffer = new byte[81920];
            int read;
            while (bytes > 0 && (read = _this.Read(buffer, 0, Math.Min(buffer.Length, bytes))) > 0)
            {
                destination.Write(buffer, 0, read);
                bytes -= read;
            }
        }
        /// <summary>
        /// Copy the specified number of bytes from this stream to the destination stream
        /// </summary>
        /// <param name="_this"></param>
        /// <param name="destination"></param>
        /// <param name="bytes"></param>
        public static async Task CopyToAsync(this Stream _this, Stream destination, int bytes, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[81920];
            int read;
            while (bytes > 0 && (read = await _this.ReadAsync(buffer, 0, Math.Min(buffer.Length, bytes), cancellationToken)) > 0)
            {
                destination.Write(buffer, 0, read);
                bytes -= read;
            }
        }
    }
}
