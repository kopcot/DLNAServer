namespace DLNAServer.Types.DLNA
{
    public static class ProtocolInfo
    {
        #region ORG_CI
        public static string EnumToString(DlnaOrgContentIndex flags)
        {
            // as DEC
            return $"{(short)flags}";
        }
        public enum DlnaOrgContentIndex : short
        {
            /// <summary>
            /// No specific index or advanced features
            /// </summary>
            NoSpecificIndex = 0,
            /// <summary>
            /// Thumbnail
            /// </summary>
            Thumbnail = 1,
        }
        #endregion ORG_CI
        #region ORG_OP
        public static string FlagsToString(DlnaOrgOperation flags)
        {
            // as BIN
            return $"{(short)flags:B2}";
        }
        [Flags]
        public enum DlnaOrgOperation : short
        {
            None = 0,                  // No operations supported
            /// <summary>
            /// Indicates whether the media server supports time-seek operations (like skipping to a specific time).
            /// </summary>
            TimeSeekSupported = 1 << 0,  // Time-based seeking supported (can jump to specific time)
            /// <summary>
            /// Indicating whether byte-range operations are supported (useful for downloading or streaming parts of the file).
            /// </summary>
            ByteSeekSupported = 1 << 1   // Byte-range seeking supported (partial content retrieval)
            // Bits 2-3 are reserved or not used in this context
        }
        #endregion ORG_OP
        #region ORG_FLAGS
        public static readonly string DefaultFlagsStreaming = FlagsToString(
            DlnaOrgFlags.ByteSeekOperation |
            DlnaOrgFlags.StreamingTransferMode |
            DlnaOrgFlags.InteractiveTransferMode |
            DlnaOrgFlags.BackgroundTransferMode |
            DlnaOrgFlags.ConnectionStall |
            DlnaOrgFlags.DlnaV15
            );
        //public static readonly string DefaultFlagsStreaming = FlagsToString(
        //    DlnaOrgFlags.StreamingTransferMode |
        //    DlnaOrgFlags.BackgroundTransferMode |
        //    DlnaOrgFlags.ConnectionStall |
        //    DlnaOrgFlags.ByteSeekOperation |
        //    DlnaOrgFlags.DlnaV15
        //    );

        public static readonly string DefaultFlagsInteractive = FlagsToString(
            DlnaOrgFlags.ByteSeekOperation |
            DlnaOrgFlags.InteractiveTransferMode |
            DlnaOrgFlags.BackgroundTransferMode |
            DlnaOrgFlags.ConnectionStall |
            DlnaOrgFlags.DlnaV15
            );

        public static string FlagsToString(DlnaOrgFlags flags)
        {
            // as HEX
            return $"{(ulong)flags:X8}{0:D24}";
        }
        [Flags]
        public enum DlnaOrgFlags : ulong
        {
            None = 0, // No flags set

            // Byte 1
            /// <summary>
            /// Indicates whether the media can be sent at a controlled pace (the server controls the flow of media).
            /// This bit is set to 1 if the server can control the transmission speed.
            /// </summary>
            SenderPaced = 1L << 31,             // Bit  0: 0x80000000
            /// <summary>
            /// Indicates whether the media server supports jumping to specific time positions within a media file, 
            /// allowing users to skip to particular timestamps during playback.
            /// </summary>
            TimeSeekOperation = 1 << 30,        // Bit  1: 0x40000000
            /// <summary>
            /// Indicates whether the media server supports requesting specific byte ranges from a media file, 
            /// enabling efficient streaming by allowing clients to download only the necessary portions of the file.
            /// </summary>
            ByteSeekOperation = 1 << 29,        // Bit  2: 0x20000000
            /// <summary>
            /// If set, the server supports containers of media items (playlists, albums, etc.).
            /// </summary>
            PlayContainer = 1 << 28,            // Bit  3: 0x10000000
            /// <summary>
            /// If this bit is set, the client can increase the speed of the stream (like fast-forward or rewind).
            /// </summary>
            S0Increase = 1 << 27,               // Bit  4: 0x08000000
            /// <summary>
            /// Similar to S0_increase, this applies to the speed of streaming (fast-forward, etc.).
            /// </summary>
            SNIncrease = 1 << 26,               // Bit  5: 0x04000000
            /// <summary>
            /// This indicates if the server supports the pause operation when using the RTSP protocol.
            /// </summary>
            RtspPause = 1 << 25,                // Bit  6: 0x02000000
            /// <summary>
            /// This bit is set if the server supports streaming transfer mode.
            /// </summary>
            StreamingTransferMode = 1 << 24,    // Bit  7: 0x01000000
            /// <summary>
            /// If set, it indicates that the server supports interactive transfer mode, where the client can request specific parts of the file.
            /// </summary>
            InteractiveTransferMode = 1 << 23,  // Bit  8: 0x00800000

            // Byte 2                                   
            /// <summary>
            /// When set, this means the media can be downloaded for playback later (background transfer).
            /// </summary>
            BackgroundTransferMode = 1 << 22,   // Bit  9: 0x00400000
            /// <summary>
            /// If set, it indicates that the server can handle stalling during playback (e.g., buffering).
            /// </summary>
            ConnectionStall = 1 << 21,          // Bit 10: 0x00200000
            /// <summary>
            /// If this bit is set, the server complies with the DLNA 1.5 standard.
            /// </summary>
            DlnaV15 = 1 << 20,                  // Bit 11: 0x00100000

            // Bits 11-31 are reserved or not used in this context
        }
        #endregion ORG_FLAGS
    }
}
