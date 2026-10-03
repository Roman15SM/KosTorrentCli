using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using KosTorrentCli.Torrent.Models;

namespace KosTorrentCli.Torrent
{
    /// <summary>
    /// UDP tracker protocol implementation (BEP 15): http://bittorrent.org/beps/bep_0015.html
    /// Most of public trackers are working via udp:// only.
    /// </summary>
    public static class UdpTrackerClient
    {
        //magic constant which identifies the protocol in connect request
        private const long ProtocolId = 0x41727101980;
        private const int ConnectAction = 0;
        private const int AnnounceAction = 1;
        private const int ErrorAction = 3;
        private const int StartedEvent = 2;
        private const int ConnectResponseLength = 16;
        //action + transaction id + interval + leechers + seeders
        private const int AnnounceResponseHeaderLength = 20;
        private const int TimeoutMs = 5000;
        private const int Attempts = 2;

        public static List<PeerResponseItem> GetPeers(Uri trackerUri, byte[] infoHash, string peerId, long left, int port)
        {
            using var client = new UdpClient();
            client.Client.ReceiveTimeout = TimeoutMs;
            client.Connect(trackerUri.Host, trackerUri.Port);

            var random = new Random();
            var connectionId = Connect(client, random);

            var transactionId = random.Next();
            var request = new List<byte>();
            request.AddRange(ToBigEndian(connectionId));
            request.AddRange(ToBigEndian(AnnounceAction));
            request.AddRange(ToBigEndian(transactionId));
            request.AddRange(infoHash);
            request.AddRange(Encoding.ASCII.GetBytes(peerId));
            //downloaded
            request.AddRange(ToBigEndian(0L));
            request.AddRange(ToBigEndian(left));
            //uploaded
            request.AddRange(ToBigEndian(0L));
            request.AddRange(ToBigEndian(StartedEvent));
            //ip address: 0 means tracker takes it from the packet
            request.AddRange(ToBigEndian(0));
            //key
            request.AddRange(ToBigEndian(random.Next()));
            //num_want: -1 means tracker default
            request.AddRange(ToBigEndian(-1));
            request.AddRange(BitConverter.GetBytes((ushort)port).Reverse());

            var response = SendAndReceive(client, request.ToArray(), AnnounceAction, transactionId, AnnounceResponseHeaderLength);

            return AnnounceResponse.ExtractPeers(response.Skip(AnnounceResponseHeaderLength).ToList());
        }

        private static long Connect(UdpClient client, Random random)
        {
            var transactionId = random.Next();
            var request = new List<byte>();
            request.AddRange(ToBigEndian(ProtocolId));
            request.AddRange(ToBigEndian(ConnectAction));
            request.AddRange(ToBigEndian(transactionId));

            var response = SendAndReceive(client, request.ToArray(), ConnectAction, transactionId, ConnectResponseLength);

            return BitConverter.ToInt64(response.Skip(8).Take(8).Reverse().ToArray());
        }

        /// <summary>
        /// UDP does not guarantee delivery, so request is resent if there is no response in time.
        /// </summary>
        private static byte[] SendAndReceive(UdpClient client, byte[] request, int expectedAction, int transactionId, int minLength)
        {
            for (var attempt = 0; attempt < Attempts; ++attempt)
            {
                client.Send(request, request.Length);

                try
                {
                    while (true)
                    {
                        var remote = new IPEndPoint(IPAddress.Any, 0);
                        var response = client.Receive(ref remote);

                        if (response.Length < 8 || ReadInt32(response, 4) != transactionId)
                            continue;

                        var action = ReadInt32(response, 0);

                        if (action == ErrorAction)
                            throw new Exception($"Tracker error: {Encoding.UTF8.GetString(response, 8, response.Length - 8)}");

                        if (action != expectedAction || response.Length < minLength)
                            throw new Exception($"Unexpected tracker response. Action: {action}, length: {response.Length}");

                        return response;
                    }
                }
                catch (SocketException e) when (e.SocketErrorCode == SocketError.TimedOut)
                {
                    //resend
                }
            }

            throw new Exception("UDP tracker did not respond");
        }

        private static int ReadInt32(byte[] data, int offset)
        {
            return BitConverter.ToInt32(data.Skip(offset).Take(4).Reverse().ToArray());
        }

        private static IEnumerable<byte> ToBigEndian(int value)
        {
            return BitConverter.GetBytes(value).Reverse();
        }

        private static IEnumerable<byte> ToBigEndian(long value)
        {
            return BitConverter.GetBytes(value).Reverse();
        }
    }
}
