using System;

namespace KosTorrentCli.Torrent.Models
{
    /// <summary>
    /// Piece which is being downloaded. Buffer is allocated once with the exact piece size,
    /// blocks are copied directly to their offsets, so they can arrive in any order.
    /// </summary>
    public class PieceProgress
    {
        public byte[] Data { get; }

        public bool[] ReceivedBlocks { get; }

        //blocks requested from the current peer and not received yet
        public bool[] RequestedBlocks { get; }

        public int ReceivedBytes { get; set; }

        public bool IsComplete => ReceivedBytes == Data.Length;

        //set by the peer which received the last block: only that peer validates and writes the piece.
        //In endgame several peers download the same piece, so blocks are written under lock(piece)
        public bool IsFinalizing { get; set; }

        public PieceProgress(int pieceLength, int blockSize)
        {
            var blockCount = (pieceLength + blockSize - 1) / blockSize;

            Data = new byte[pieceLength];
            ReceivedBlocks = new bool[blockCount];
            RequestedBlocks = new bool[blockCount];
        }

        /// <summary>
        /// Peer discards pending requests on choke and a new peer knows nothing about previous requests,
        /// so not received blocks have to be requested again.
        /// </summary>
        public void ResetRequests()
        {
            Array.Clear(RequestedBlocks);
        }
    }
}
