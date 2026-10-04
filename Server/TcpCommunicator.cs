using KosTorrentCli.Torrent.Models;
using KosTorrentCli.Torrent;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;

namespace KosTorrentCli.Server
{
    public class TcpCommunicator
    {
        //de facto standard block size: most clients drop the connection on requests larger than 16kB
        private const int BlockSize = 16384;
        //pipelined block requests per peer (1MB). Peers usually limit request queue to ~250,
        //and requesting everything at once keeps all pieces of the torrent in memory
        private const int MaxOutstandingRequests = 64;
        private const int PieceHashLength = 20;
        private const int IntByteLength = 4;
        private const int PieceHeaderSize = 13;
        //protection from garbage length prefix which would lead to huge allocation
        private const int MaxMessageLength = 2 * 1024 * 1024;
        private const int ConnectTimeoutMs = 5000;
        private const int ReceiveTimeoutMs = 30000;
        //peer which keeps us choked or does not send any piece data is abandoned after this time
        private static readonly TimeSpan PeerIdleTimeout = TimeSpan.FromSeconds(60);

        /// <summary>
        /// State of the communication with a single peer
        /// </summary>
        private class PeerState
        {
            //"ip:port", used in logs to see which peer the data came from
            public string Peer { get; init; }

            public bool IsChoked { get; set; } = true;

            public bool IsInterestedSent { get; set; }

            public int OutstandingRequests { get; set; }

            //pieces which peer has, and which are not started yet. Sorted to download file sequentially.
            //Pieces owned by other peers stay here: they are released if the owner disconnects
            public SortedSet<int> AvailablePieces { get; } = new SortedSet<int>();

            //pieces owned by this peer: only this peer downloads them and modifies their PieceProgress
            public List<int> ActivePieces { get; } = new List<int>();

            //bitfield or have message arrived, so we know which pieces the peer has
            public bool HasPieceInfo { get; set; }

            //shared by all peers, cancelled once the last piece is downloaded
            public CancellationTokenSource DownloadCompleted { get; init; }
        }

        public void DownloadTorrent(string endpoint, 
            int port, 
            byte[] message, 
            TorrentMetaInfo metaInfo, 
            ConcurrentDictionary<int, PieceProgress> allData, 
            ConcurrentDictionary<int, int> alreadyDownloadedPieces,
            FileCreator creator,
            CancellationTokenSource downloadCompleted)
        {
            var listener = new TcpClient();
            var state = new PeerState
            {
                Peer = $"{endpoint}:{port}",
                DownloadCompleted = downloadCompleted
            };

            //closing the socket interrupts blocking Connect/Read, so peers stop as soon as the whole torrent is downloaded
            using var completedRegistration = downloadCompleted.Token.Register(() => listener.Close());

            try
            {
                if (!listener.ConnectAsync(endpoint, port).Wait(ConnectTimeoutMs))
                {
                    Console.WriteLine($"Peer {endpoint}:{port} connection timeout");
                    return;
                }

                //without timeouts Read blocks forever on a silent peer
                listener.ReceiveTimeout = ReceiveTimeoutMs;
                listener.SendTimeout = ReceiveTimeoutMs;

                if (!IsHandShakeSuccessful(listener, message))
                    return;

                var stream = listener.GetStream();
                var lastPieceDataTime = DateTime.UtcNow;

                var bitFieldrequest = MessageGenerator.GenerateBitFieldRequest(metaInfo.Info.PieceCount, alreadyDownloadedPieces);
                stream.Write(bitFieldrequest, 0, bitFieldrequest.Length);

                while (!downloadCompleted.IsCancellationRequested)
                {
                    if (HasNothingToDownload(state, alreadyDownloadedPieces))
                    {
                        Console.WriteLine($"Peer {endpoint}:{port} has no more pieces we need");
                        return;
                    }

                    if (DateTime.UtcNow - lastPieceDataTime > PeerIdleTimeout)
                    {
                        Console.WriteLine($"Peer {endpoint}:{port} did not send any piece data for {PeerIdleTimeout.TotalSeconds}s");
                        return;
                    }

                    //message: <length prefix><message ID><payload>
                    var lengthPrefix = new byte[IntByteLength];

                    if (!ReadExactly(stream, lengthPrefix, 0, IntByteLength))
                    {
                        Console.WriteLine($"Peer {endpoint}:{port} closed the connection");
                        return;
                    }

                    var length = MessageParser.GetBlockLength(lengthPrefix, 0);

                    //keep-alive message
                    if (length == 0)
                        continue;

                    if (length < 0 || length > MaxMessageLength)
                        throw new Exception($"Wrong communication block length arrived: {length}");

                    var communicationBlock = new byte[IntByteLength + length];
                    Array.Copy(lengthPrefix, communicationBlock, IntByteLength);

                    if (!ReadExactly(stream, communicationBlock, IntByteLength, length))
                    {
                        Console.WriteLine($"Peer {endpoint}:{port} closed the connection");
                        return;
                    }

                    var messageId = (PeerMessageType)communicationBlock[4];

                    //piece messages are 16kB blocks (64 per 1MB piece), progress is logged per completed piece instead
                    if (messageId == PeerMessageType.Piece)
                        lastPieceDataTime = DateTime.UtcNow;
                    else
                        Console.WriteLine($"Peer {endpoint}:{port} Message Id: {messageId}");

                    MessageProcessor(communicationBlock, stream, metaInfo, alreadyDownloadedPieces, communicationBlock.Length, messageId, allData, creator, state);
                }
            }
            catch (Exception) when (downloadCompleted.IsCancellationRequested)
            {
                //socket was closed because the whole torrent is downloaded
            }
            catch (Exception e) when (IsConnectionError(e))
            {
                //refused/reset connections are usual for peers, so there is no need for the whole stack trace
                Console.WriteLine($"Peer {endpoint}:{port} connection failed: {(e.InnerException ?? e).Message}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Peer {endpoint}:{port} failed: {e}");
            }
            finally
            {
                //release unfinished pieces, so other peers can claim them
                foreach (var activePiece in state.ActivePieces)
                {
                    allData.TryRemove(activePiece, out _);
                }

                listener.Close();
            }
        }

        /// <summary>
        /// Peer is useless when everything it has is already downloaded (possibly by other peers).
        /// Checked only when nothing is in flight, since it walks over all available pieces.
        /// </summary>
        private static bool HasNothingToDownload(PeerState state, ConcurrentDictionary<int, int> alreadyDownloadedPieces)
        {
            if (!state.HasPieceInfo || state.ActivePieces.Count > 0 || state.OutstandingRequests > 0)
                return false;

            state.AvailablePieces.RemoveWhere(alreadyDownloadedPieces.ContainsKey);

            return state.AvailablePieces.Count == 0;
        }

        private static bool IsConnectionError(Exception e)
        {
            if (e is AggregateException aggregate && aggregate.InnerException != null)
                e = aggregate.InnerException;

            //file errors are IOException too, but only network ones wrap SocketException
            return e is SocketException || e is IOException { InnerException: SocketException };
        }

        /// <summary>
        /// TCP is a stream: a single Read can return only a part of a message, so read until the whole count arrives.
        /// Returns false if peer closed the connection (Read returned 0 bytes).
        /// </summary>
        private static bool ReadExactly(NetworkStream stream, byte[] buffer, int offset, int count)
        {
            while (count > 0)
            {
                var read = stream.Read(buffer, offset, count);

                if (read == 0)
                    return false;

                offset += read;
                count -= read;
            }

            return true;
        }

        private void MessageProcessor(byte[] data, 
            NetworkStream stream, 
            TorrentMetaInfo metaInfo, 
            ConcurrentDictionary<int, int> alreadyDownloadedPieces, 
            int bytes, 
            PeerMessageType messageId, 
            ConcurrentDictionary<int, PieceProgress> allData, 
            FileCreator creator, 
            PeerState state)
        {
            switch (messageId)
            {
                case PeerMessageType.Choke:
                    //peer discards all pending requests on choke
                    state.IsChoked = true;
                    state.OutstandingRequests = 0;

                    foreach (var activePiece in state.ActivePieces)
                    {
                        if (allData.TryGetValue(activePiece, out var progress))
                            progress.ResetRequests();
                    }
                    break;
                case PeerMessageType.Have:
                    var pieceNumber = MessageParser.GetPieceIndex(data);

                    if (!alreadyDownloadedPieces.ContainsKey(pieceNumber) && !state.ActivePieces.Contains(pieceNumber))
                        state.AvailablePieces.Add(pieceNumber);

                    state.HasPieceInfo = true;
                    SendInterested(stream, state);
                    FillRequests(stream, metaInfo, allData, alreadyDownloadedPieces, state);
                    break;
                case PeerMessageType.UnChoke:
                    //var random = new Random();
                    //var nextPieceToRequest = random.Next(bitfieldAvailablePieces.Count - 1);
                    //var requestRequest = MessageGenerator.GenerateRequestRequest(nextPieceToRequest, 0, _blockSize);
                    //stream.Write(requestRequest, 0, requestRequest.Length);
                    //Console.WriteLine($"Sent piece request {nextPieceToRequest}");
                    state.IsChoked = false;
                    SendInterested(stream, state);
                    FillRequests(stream, metaInfo, allData, alreadyDownloadedPieces, state);
                    break;
                case PeerMessageType.Piece:
                    state.OutstandingRequests = Math.Max(0, state.OutstandingRequests - 1);
                    ProcessBlock(data, metaInfo, alreadyDownloadedPieces, allData, creator, state);
                    FillRequests(stream, metaInfo, allData, alreadyDownloadedPieces, state);
                    break;
                case PeerMessageType.BitField:
                    var body = MessageParser.GetBitFieldBody(data);
                    var availablePieces = 0;

                    for (var i = 0; i < body.Length; ++i)
                    {
                        //leading zeros are significant: first bit of the byte is piece i*8
                        var bitVersion = Convert.ToString(body[i], 2).PadLeft(8, '0');

                        for (var j = 0; j < bitVersion.Length; ++j)
                        {
                            var pieceId = i * 8 + j;

                            //spare bits at the end of the last byte
                            if (pieceId >= metaInfo.Info.PieceCount)
                                break;

                            if (bitVersion[j] == '1' && !alreadyDownloadedPieces.ContainsKey(pieceId))
                            {
                                state.AvailablePieces.Add(pieceId);
                                ++availablePieces;
                            }
                        }
                    }

                    Console.WriteLine($"Peer {state.Peer} has {availablePieces} pieces we need");
                    state.HasPieceInfo = true;
                    SendInterested(stream, state);
                    break;
                default:
                    Console.WriteLine($"Peer {state.Peer} unsupported message {(int)messageId}: {Encoding.ASCII.GetString(data, 0, bytes)}");
                    break;
            }
        }

        private void ProcessBlock(byte[] data, 
            TorrentMetaInfo metaInfo, 
            ConcurrentDictionary<int, int> alreadyDownloadedPieces, 
            ConcurrentDictionary<int, PieceProgress> allData, 
            FileCreator creator, 
            PeerState state)
        {
            var pieceIndex = MessageParser.GetPieceIndex(data);
            var blockOffset = MessageParser.GetBlockOffset(data);
            var blockLength = data.Length - PieceHeaderSize;

            //accept blocks only for pieces owned by this peer: a late block of a released or discarded piece
            //must not be written into the progress of another peer which claimed that piece since
            if (!state.ActivePieces.Contains(pieceIndex) || !allData.TryGetValue(pieceIndex, out var piece))
                return;

            var blockIndex = blockOffset / BlockSize;

            if (blockOffset % BlockSize != 0 || blockIndex >= piece.ReceivedBlocks.Length
                || blockLength != Math.Min(BlockSize, piece.Data.Length - blockOffset))
            {
                Console.WriteLine($"Peer {state.Peer} sent unexpected block of piece {pieceIndex}: offset {blockOffset}, length {blockLength}");
                return;
            }

            //duplicate
            if (piece.ReceivedBlocks[blockIndex])
                return;

            Buffer.BlockCopy(data, PieceHeaderSize, piece.Data, blockOffset, blockLength);
            piece.ReceivedBlocks[blockIndex] = true;
            piece.ReceivedBytes += blockLength;

            if (!piece.IsComplete)
                return;

            //ownership in allData is released only after the piece is marked as downloaded (or rejected):
            //while hashing and writing, other peers must still see the piece as owned, otherwise they claim and download it again
            state.ActivePieces.Remove(pieceIndex);

            if (!ValidatePiece(metaInfo.Info.PiecesBytes.Skip(pieceIndex * PieceHashLength).Take(PieceHashLength).ToArray(), piece.Data))
            {
                //piece is not requested from this peer again, another peer will claim it
                allData.TryRemove(pieceIndex, out _);
                Console.WriteLine($"Peer {state.Peer} sent invalid piece {pieceIndex} (SHA-1 mismatch)");
                return;
            }

            //piece is marked as downloaded only after it is written, otherwise a failed write leaves a hole in the file forever
            try
            {
                creator.AllocatePiece(piece.Data, metaInfo, pieceIndex);
            }
            catch (IOException e)
            {
                allData.TryRemove(pieceIndex, out _);
                Console.WriteLine($"Piece {pieceIndex} from {state.Peer} was not saved, it will be downloaded again: {e.Message}");
                state.AvailablePieces.Add(pieceIndex);
                return;
            }

            alreadyDownloadedPieces.TryAdd(pieceIndex, pieceIndex);
            allData.TryRemove(pieceIndex, out _);
            //single line: with parallel peers two separate WriteLine calls get interleaved with other peers' output
            Console.WriteLine($"Piece {pieceIndex} downloaded from {state.Peer}, size: {piece.Data.Length} ({alreadyDownloadedPieces.Count}/{metaInfo.Info.PieceCount})");

            if (alreadyDownloadedPieces.Count == metaInfo.Info.PieceCount)
                state.DownloadCompleted.Cancel();
        }

        /// <summary>
        /// Keeps up to MaxOutstandingRequests block requests in flight.
        /// The next piece is started only when all blocks of active pieces are requested,
        /// so only a few pieces are kept in memory.
        /// </summary>
        private void FillRequests(NetworkStream stream, TorrentMetaInfo metaInfo, ConcurrentDictionary<int, PieceProgress> allData, ConcurrentDictionary<int, int> alreadyDownloadedPieces, PeerState state)
        {
            if (state.IsChoked)
                return;

            while (state.OutstandingRequests < MaxOutstandingRequests)
            {
                if (!TryGetNextBlock(metaInfo, allData, alreadyDownloadedPieces, state, out var piece, out var pieceIndex, out var blockIndex))
                    return;

                var begin = blockIndex * BlockSize;
                var length = Math.Min(BlockSize, piece.Data.Length - begin);

                piece.RequestedBlocks[blockIndex] = true;
                ++state.OutstandingRequests;

                var request = MessageGenerator.GenerateRequestRequest(pieceIndex, begin, length);
                stream.Write(request, 0, request.Length);
            }
        }

        private bool TryGetNextBlock(TorrentMetaInfo metaInfo,
            ConcurrentDictionary<int, PieceProgress> allData,
            ConcurrentDictionary<int, int> alreadyDownloadedPieces,
            PeerState state,
            out PieceProgress piece,
            out int pieceIndex,
            out int blockIndex)
        {
            foreach (var activePiece in state.ActivePieces)
            {
                if (!allData.TryGetValue(activePiece, out piece))
                    continue;

                for (var i = 0; i < piece.ReceivedBlocks.Length; ++i)
                {
                    if (!piece.ReceivedBlocks[i] && !piece.RequestedBlocks[i])
                    {
                        pieceIndex = activePiece;
                        blockIndex = i;
                        return true;
                    }
                }
            }

            piece = null;
            pieceIndex = -1;
            blockIndex = -1;

            if (!TryClaimNextPiece(metaInfo, allData, alreadyDownloadedPieces, state))
                return false;

            return TryGetNextBlock(metaInfo, allData, alreadyDownloadedPieces, state, out piece, out pieceIndex, out blockIndex);
        }

        /// <summary>
        /// Every piece is downloaded by a single peer: the peer whose TryAdd succeeded owns it.
        /// Pieces owned by other peers are skipped but kept, since the owner can disconnect and release them.
        /// </summary>
        private bool TryClaimNextPiece(TorrentMetaInfo metaInfo, ConcurrentDictionary<int, PieceProgress> allData, ConcurrentDictionary<int, int> alreadyDownloadedPieces, PeerState state)
        {
            var downloaded = new List<int>();
            var claimed = -1;

            foreach (var candidate in state.AvailablePieces)
            {
                if (alreadyDownloadedPieces.ContainsKey(candidate))
                {
                    downloaded.Add(candidate);
                    continue;
                }

                //cheap check first: PieceProgress allocates the whole piece buffer
                if (allData.ContainsKey(candidate))
                    continue;

                if (allData.TryAdd(candidate, new PieceProgress(metaInfo.Info.GetPieceLength(candidate), BlockSize)))
                {
                    claimed = candidate;
                    break;
                }
            }

            foreach (var piece in downloaded)
            {
                state.AvailablePieces.Remove(piece);
            }

            if (claimed < 0)
                return false;

            state.AvailablePieces.Remove(claimed);
            state.ActivePieces.Add(claimed);

            return true;
        }

        private static void SendInterested(NetworkStream stream, PeerState state)
        {
            if (state.IsInterestedSent)
                return;

            var interested = MessageGenerator.GenerateInterestedRequest();
            stream.Write(interested, 0, interested.Length);
            state.IsInterestedSent = true;
        }

        private bool ValidatePiece(byte[] validPieceHash, byte[] pieceData)
        {
            var hash = SHA1.HashData(pieceData);

            return hash.SequenceEqual(validPieceHash);
        }

        private bool IsHandShakeSuccessful(TcpClient listener, byte[] message)
        {
            var stream = listener.GetStream();
            stream.Write(message, 0, message.Length);

            var data = new byte[message.Length];

            if (!ReadExactly(stream, data, 0, data.Length))
                return false;

            if (!VerifyHandShake(message, data))
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
