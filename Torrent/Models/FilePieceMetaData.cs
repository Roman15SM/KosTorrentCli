namespace KosTorrentCli.Torrent.Models
{
    public class FilePieceMetaData
    {
        public int StartPieceNumber { get; set; }

        public long FileSize { get; set; }

        public string Path { get; set; }
    }
}
