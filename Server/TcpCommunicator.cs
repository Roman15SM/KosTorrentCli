using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using KosTorrentCli.Torrent;
using KosTorrentCli.Torrent.Models;

namespace KosTorrentCli.Server
{
    public class TcpCommunicator
    {
        //max limit per documentation: 128kB
        private int BlockSize = 32768*4;
        private const int PieceHashLength = 20;
        private const int MinMessageLength = 5;
        private const int IntByteLength = 4;
        private const int PieceHeaderSize = 13;

        public void DownloadTorrent(string endpoint, int port, byte[] message, TorrentMetaInfo metaInfo, Dictionary<int, List<byte>> allData, HashSet<int> alreadyDownloadedPieces)
        {
            var listener = new TcpClient();

            if (metaInfo.Info.PieceLength < BlockSize)
                BlockSize = metaInfo.Info.PieceLength;

            try
            {
                listener.Connect(endpoint, port);

                if (!IsHandShakeSuccessful(listener, message))
                    return;

                var stream = listener.GetStream();
                var data = new byte[BlockSize];
                var pieceAmount = metaInfo.Info.PiecesBytes.Count / PieceHashLength - alreadyDownloadedPieces.Count;
                var pieceIterator = 0;
                var isMessageTail = false;
                var messagePrefix = new List<byte>();
                var tailLength = 0;

                while (pieceIterator < pieceAmount)
                {
                    var filledBufferLength = stream.Read(data, 0, BlockSize);

                    if (filledBufferLength == 0)
                        continue;

                    var offset = 0;
                    
                    if (isMessageTail)
                    {
                        if (tailLength > filledBufferLength)
                        {
                            messagePrefix.AddRange(data.Take(filledBufferLength));
                            tailLength -= filledBufferLength;
                            continue;
                        }

                        isMessageTail = false;
                        messagePrefix.AddRange(data.Take(tailLength));
                        offset += tailLength;
                        tailLength = 0;

                        var messageId = (PeerMessageType)Enum.Parse(typeof(PeerMessageType), messagePrefix[4].ToString());
                        Console.WriteLine($"Message Id: {messageId}");
                        MessageProcessor(messagePrefix.ToArray(), stream, metaInfo, alreadyDownloadedPieces, messagePrefix.Count, messageId, allData, ref pieceIterator, ref pieceAmount);
                    }

                    if (filledBufferLength < MinMessageLength)
                        continue;

                    while (offset < filledBufferLength)
                    {
                        var length = MessageParser.GetBlockLength(data, offset);

                        if (length == 0)
                        {
                            offset += IntByteLength;
                            continue;
                        }

                        var blockLength = length + IntByteLength;
                        var communicationBlock = data.Skip(offset).Take(blockLength).ToArray();

                        if (offset + blockLength > filledBufferLength)
                        {
                            isMessageTail = true;
                            tailLength = Math.Abs(filledBufferLength - blockLength - offset);
                        }

                        if (communicationBlock.Length <= IntByteLength)
                            throw new Exception($"Wrong communication block arrived: {Encoding.ASCII.GetString(communicationBlock)}");

                        var messageId = (PeerMessageType)Enum.Parse(typeof(PeerMessageType), communicationBlock[4].ToString());
                        Console.WriteLine($"Message Id: {messageId}");

                        if (isMessageTail)
                        {
                            messagePrefix = communicationBlock.Take(blockLength - tailLength).ToList();
                        }
                        else
                        {
                            MessageProcessor(communicationBlock, stream, metaInfo, alreadyDownloadedPieces, blockLength, messageId, allData, ref pieceIterator, ref pieceAmount);
                        }

                        offset += blockLength;
                    }
                }

                Console.WriteLine("Downloaded all possible pieces from peer");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                listener.Client.Close();
                listener.Close();
            }
        }

        private void MessageProcessor(byte[] data, NetworkStream stream, TorrentMetaInfo metaInfo, HashSet<int> alreadyDownloadedPieces, int bytes, PeerMessageType messageId, Dictionary<int, List<byte>> allData, ref int pieceIterator, ref int pieceAmount)
        {
            switch (messageId)
            {
                case PeerMessageType.Choke:
                    break;
                case PeerMessageType.Have:
                    var pieceNumber = MessageParser.GetPieceIndex(data);

                    if (!alreadyDownloadedPieces.Contains(pieceNumber))
                    {
                        var request = MessageGenerator.GenerateRequestRequest(pieceNumber, 0, BlockSize);
                        stream.Write(request, 0, request.Length);
                    }
                    break;
                case PeerMessageType.UnChoke:
                    var interested = MessageGenerator.GenerateInterestedRequest();
                    stream.Write(interested, 0, interested.Length);
                    break;
                case PeerMessageType.Piece:
                    var pieceIndex = MessageParser.GetPieceIndex(data);

                    if (alreadyDownloadedPieces.Contains(pieceIndex))
                        return;

                    if (!allData.ContainsKey(pieceIndex))
                        allData[pieceIndex] = new List<byte>();

                    allData[pieceIndex].AddRange(data.Skip(PieceHeaderSize));

                    if (allData[pieceIndex].Count == metaInfo.Info.PieceLength)
                    {
                        if (!ValidatePiece(
                                metaInfo.Info.PiecesBytes.Skip(pieceIndex * PieceHashLength).Take(PieceHashLength).ToArray(),
                                allData[pieceIndex]))
                        {
                            Console.WriteLine($"Invalid piece discovered. Piece index: {pieceIndex}");
                            allData[pieceIndex] = new List<byte>();
                            return;
                        }

                        ++pieceIterator;
                        alreadyDownloadedPieces.Add(pieceIndex);
                        Console.WriteLine($"Piece {pieceIndex} is fully downloaded, size: {allData[pieceIndex].Count}");
                        Console.WriteLine($"Downloaded ({alreadyDownloadedPieces.Count}/{metaInfo.Info.PiecesBytes.Count / PieceHashLength})");
                    }
                    else
                    {
                        Console.WriteLine($"Piece {pieceIndex} updated, downloaded: {allData[pieceIndex].Count}");

                        var request = MessageGenerator.GenerateRequestRequest(pieceIndex, metaInfo.Info.PieceLength - allData[pieceIndex].Count, BlockSize);
                        stream.Write(request, 0, request.Length);
                    }
                    break;
                case PeerMessageType.BitField:
                    var body = MessageParser.GetBitFieldBody(data);
                    var availablePieces = -1;

                    for (var i = 0; i < body.Length; ++i)
                    {
                        var bitVersion = Convert.ToString(body[i], 2);
                        
                        for (var j = 0; j < bitVersion.Length; ++j)
                        {
                            if (bitVersion[j] == '1')
                            {
                                var pieceId = i * 8 + j;

                                if (!alreadyDownloadedPieces.Contains(pieceId))
                                {
                                    var request = MessageGenerator.GenerateRequestRequest(i * 8 + j, 0, BlockSize);
                                    stream.Write(request, 0, request.Length);
                                    ++availablePieces;
                                }
                            }
                        }
                    }

                    pieceAmount = availablePieces;
                    break;
                default:
                    Console.WriteLine(Encoding.ASCII.GetString(data, 0, bytes));
                    break;
            }
        }

        private bool ValidatePiece(byte[] validPieceHash, List<byte> pieceData)
        {
            var processor = new Processor();
            var infoHash = processor.GenerateSha1Hash(pieceData.ToArray());

            if (infoHash.Length != PieceHashLength)
                return false;

            for (var i = 0; i < infoHash.Length; ++i)
            {
                if (infoHash[i] != validPieceHash[i])
                    return false;
            }

            return true;
        }

        private bool IsHandShakeSuccessful(TcpClient listener, byte[] message)
        {
            var stream = listener.GetStream();
            stream.Write(message, 0, message.Length);

            var data = new byte[message.Length];
            var bytes = stream.Read(data, 0, data.Length);

            if (!VerifyHandShake(message, data.Take(bytes).ToArray()))
                return false;

            return true;
        }

        private bool VerifyHandShake(byte[] request, byte[] response)
        {
            if (request.Length != response.Length)
                return false;

            //PeerId in handshake response is 20 byte length respondent's peer id.
            const int peerIdLength = 20;

            for (var i = request.Length - 2*peerIdLength; i < request.Length - peerIdLength; ++i)
            {
                if (request[i] != response[i])
                    return false;
            }

            return true;
        }
    }
}
