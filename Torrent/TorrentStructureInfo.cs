using System.Collections.Generic;

namespace KosTorrentCli.Torrent
{
    public class TorrentStructureInfo
    {
        public int PieceLength { get; set; }

        public bool IsPrivate { get; set; }

        public string Name { get; set; }

        public List<byte> BencodeByteData { get; set; }

        public long TotalLength { get; set; }

        public List<TorrentFilePieceInfo> Pieces { get; set; }
    }
}
