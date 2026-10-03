using System.Collections.Generic;

namespace KosTorrentCli.Torrent.Models
{
    public class TorrentStructureInfo
    {
        public int PieceLength { get; set; }

        public bool IsPrivate { get; set; }

        public string Name { get; set; }

        public List<byte> BencodeByteData { get; set; }

        public long TotalLength { get; set; }

        public List<TorrentFilePieceInfo> Pieces { get; set; }

        public List<byte> PiecesBytes { get; set; }

        //each piece is described by its 20 bytes SHA-1 hash
        public int PieceCount => PiecesBytes.Count / 20;

        /// <summary>
        /// All pieces have PieceLength size except the last one, which holds the remainder of TotalLength.
        /// </summary>
        public int GetPieceLength(int pieceIndex)
        {
            if (pieceIndex < PieceCount - 1)
                return PieceLength;

            return (int)(TotalLength - (long)pieceIndex * PieceLength);
        }
    }
}
