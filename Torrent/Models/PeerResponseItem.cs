namespace KosTorrentCli.Torrent.Models
{
    public class PeerResponseItem
    {
        public string PeerId { get; set; }

        //peer's IP address either IPv6 (hexed) or IPv4 (dotted quad) or DNS name (string)
        public string PeerIp { get; set; }

        public int Port { get; set; }
    }
}
