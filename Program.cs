using System.Linq;
using KosTorrentCli.Bencode;
using KosTorrentCli.Torrent;
using KosTorrentCli.Torrent.Models;

namespace KosTorrentCli
{
    class Program
    {
        /// <summary>
        /// For now, path to torrent file is passed as a first console parameter
        /// For debug purposes, you can set it up in KosTorrentCli project properties => Debug => Application arguments
        /// All necessary validations + unit tests will come soon
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            var parser = new Parser();
            var path = args[0];
            var torrentDataTrie = parser.Parse(path);
            var torrentMetaData = new TorrentMetaInfo(torrentDataTrie);

            var processor = new Processor();
            var infoHash = processor.GenerateSha1Hash(torrentMetaData.Info.BencodeByteData.ToArray());

            var peers = processor.GetPeers(torrentMetaData, infoHash);

            //For testing purpose
            var testPeer = peers.First();
            var handshakeMessage = new PeerHandShake(PeerIdGenerator.GetPeerId(), infoHash).GenerateHandShakeMessage();
        }
    }
}
