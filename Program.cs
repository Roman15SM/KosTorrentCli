using KosTorrentCli.Bencode;
using KosTorrentCli.Torrent;
using KosTorrentCli.Torrent.Models;

namespace KosTorrentCli
{
    class Program
    {
        static void Main(string[] args)
        {
            var parser = new Parser();
            var path = args[0];
            var torrentDataTrie = parser.Parse(path);
            var torrentMetaData = new TorrentMetaInfo(torrentDataTrie);

            var processor = new Processor();
            processor.GetPeers(torrentMetaData);
        }
    }
}
