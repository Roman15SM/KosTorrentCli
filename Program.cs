using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KosTorrentCli.Bencode;
using KosTorrentCli.Server;
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
            //TcpRule.AddTcpRule();

            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += new UnhandledExceptionEventHandler(GlobalErrorHandler);
            var peerId = PeerIdGenerator.GetPeerId();
            var parser = new Parser();
            var path = args[0];
            var torrentDataTrie = parser.Parse(path);
            var torrentMetaData = new TorrentMetaInfo(torrentDataTrie);

            var processor = new Processor();
            var infoHash = processor.GenerateSha1Hash(torrentMetaData.Info.BencodeByteData.ToArray());

            var peers = processor.GetPeers(torrentMetaData, infoHash, peerId);
            var handshakeMessage = new PeerHandShake(peerId, infoHash).GenerateHandShakeMessage();
            var communicator = new TcpCommunicator();
            var allData = new Dictionary<int, List<byte>>();
            var alreadyDownloadedPieces = new HashSet<int>();

            if (peers == null || !peers.Any())
            {
                Console.WriteLine("No peer available");
                return;
            }

            var creator = new FileCreator();
            creator.GenerateFolderStructure(torrentMetaData);

            foreach (var peer in peers)
            {
                communicator.DownloadTorrent(peer.PeerIp, peer.Port, handshakeMessage, torrentMetaData, allData, alreadyDownloadedPieces, creator);
            }
        }

        static void GlobalErrorHandler(object sender, UnhandledExceptionEventArgs args)
        {
            Exception e = (Exception)args.ExceptionObject;
            Console.WriteLine("GlobalErrorHandler caught : " + e.Message);
            Console.WriteLine("Runtime terminating: {0}", args.IsTerminating);
        }
    }
}
