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

            foreach (var peer in peers)
            {
                communicator.DownloadTorrent(peer.PeerIp, peer.Port, handshakeMessage, torrentMetaData, allData, alreadyDownloadedPieces);
            }

            //happy way
            var currentFilePieces = torrentMetaData.Info.Pieces.Count > 0
                ? torrentMetaData.Info.Pieces
                : new List<TorrentFilePieceInfo>
                {
                    new TorrentFilePieceInfo
                    {
                        Length = allData.Count * torrentMetaData.Info.PieceLength,
                        Path = new List<string>
                        {
                            torrentMetaData.Info.Name
                        }
                    }
                };

            foreach (var piece in currentFilePieces)
            {
                if (piece.Path.Count <= 1)
                    continue;

                var piecePath = string.Empty;

                for(var i = 0; i < piece.Path.Count - 1; ++i)
                {
                    piecePath += piece.Path[i] + "/";
                }

                Directory.CreateDirectory(piecePath);
                piece.Path[0] = piecePath + piece.Path.Last();
            }

            var leftOver = new List<byte>();
            var iterator = 0;
            var downloadedAmount = 0;

            foreach (var piece in currentFilePieces)
            {
                using (var stream = new FileStream(piece.Path[0], FileMode.Append))
                {
                    if (leftOver.Count > 0)
                    {
                        if (leftOver.Count > piece.Length)
                        {
                            stream.Write(leftOver.Take(leftOver.Count - piece.Length).ToArray(), 0, leftOver.Count - piece.Length);
                            leftOver = leftOver.Skip(leftOver.Count - piece.Length).ToList();
                            downloadedAmount += piece.Length;
                            continue;
                        }
                        
                        stream.Write(leftOver.ToArray(), 0, leftOver.Count);
                        leftOver = new List<byte>();
                    }

                    for (var i = iterator; i < allData.Count; ++i)
                    {
                        var amountToPush = iterator * allData[i].Count - downloadedAmount;
                        ++iterator;

                        if (amountToPush > piece.Length)
                        {
                            stream.Write(allData[i].Take(amountToPush - piece.Length).ToArray(), 0, amountToPush - piece.Length);
                            leftOver = allData[i].Skip(amountToPush - piece.Length).ToList();
                            downloadedAmount += piece.Length;
                            break;
                        }

                        stream.Write(allData[i].ToArray(), 0, allData[i].Count);
                    }
                }
            }
        }

        static void GlobalErrorHandler(object sender, UnhandledExceptionEventArgs args)
        {
            Exception e = (Exception)args.ExceptionObject;
            Console.WriteLine("MyHandler caught : " + e.Message);
            Console.WriteLine("Runtime terminating: {0}", args.IsTerminating);
        }
    }
}
