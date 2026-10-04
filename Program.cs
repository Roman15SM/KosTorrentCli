using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KosTorrentCli.Bencode;
using KosTorrentCli.Server;
using KosTorrentCli.Torrent;
using KosTorrentCli.Torrent.Models;

namespace KosTorrentCli
{
    class Program
    {
        //peers connected at the same time
        private const int MaxConnectedPeers = 25;

        /// <summary>
        /// For now, path to torrent file is passed as a first console parameter
        /// For debug purposes, you can set it up in KosTorrentCli project properties => Debug => Application arguments
        /// All necessary validations + unit tests will come soon
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            TcpRule.AddTcpRule();

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
            var allData = new ConcurrentDictionary<int, PieceProgress>();
            var alreadyDownloadedPieces = new ConcurrentDictionary<int, int>();

            if (peers == null || !peers.Any())
            {
                Console.WriteLine(GetNoPeersReason(torrentMetaData, torrentDataTrie));
                return;
            }

            var creator = new FileCreator();
            creator.GenerateFolderStructure(torrentMetaData);

            //peers are served by blocking sockets, so thread pool must not wait to inject threads for them
            ThreadPool.GetMinThreads(out var workerThreads, out var ioThreads);
            ThreadPool.SetMinThreads(Math.Max(workerThreads, MaxConnectedPeers), ioThreads);

            using var downloadCompleted = new CancellationTokenSource();
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = MaxConnectedPeers };

            Parallel.ForEach(peers, parallelOptions, (peer, loopState) =>
            {
                //no new peers once the whole torrent is downloaded
                if (downloadCompleted.IsCancellationRequested)
                {
                    loopState.Stop();
                    return;
                }

                communicator.DownloadTorrent(peer.PeerIp, peer.Port, handshakeMessage, torrentMetaData, allData, alreadyDownloadedPieces, creator, downloadCompleted);
            });

            if (alreadyDownloadedPieces.Count == torrentMetaData.Info.PieceCount)
                Console.WriteLine("Download completed");
            else
                Console.WriteLine($"Download is not completed: {alreadyDownloadedPieces.Count}/{torrentMetaData.Info.PieceCount} pieces. No more peers available");
        }

        /// <summary>
        /// Trackerless torrents (e.g. Arch Linux ISO) get peers only via DHT and/or download from web seeds,
        /// both are not supported yet, so explain it instead of a bare "no peers".
        /// </summary>
        static string GetNoPeersReason(TorrentMetaInfo metaInfo, TorrentDataTrie trie)
        {
            var hasTrackers = !string.IsNullOrWhiteSpace(metaInfo.AnnounceUrl) || metaInfo.AnnounceList.Any(url => !string.IsNullOrWhiteSpace(url));

            if (hasTrackers)
                return "No peers: trackers returned no peers (they may be down, or nobody is sharing this torrent right now).";

            var hasWebSeeds = trie.GetItem("url-list").Type != TorrentMetaType.Unset;

            return hasWebSeeds
                ? "No peers: this torrent has no trackers. It relies on DHT and web seeds (HTTP mirrors), which are not supported yet."
                : "No peers: this torrent has no trackers. It relies on DHT, which is not supported yet.";
        }

        static void GlobalErrorHandler(object sender, UnhandledExceptionEventArgs args)
        {
            Exception e = (Exception)args.ExceptionObject;
            Console.WriteLine("GlobalErrorHandler caught : " + e.Message);
            Console.WriteLine("Runtime terminating: {0}", args.IsTerminating);
        }
    }
}
