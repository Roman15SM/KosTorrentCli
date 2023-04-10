using System.Collections.Generic;

namespace KosTorrentCli.Torrent.Models
{
    public class TorrentFilePieceInfo
    {
        public long Length { get; set; }

        public string MdSum { get; set; }

        public List<string> Path { get; set; }
    }
}
