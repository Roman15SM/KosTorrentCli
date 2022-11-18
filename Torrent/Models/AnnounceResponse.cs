using System;
using System.Collections.Generic;
using KosTorrentCli.Bencode;

namespace KosTorrentCli.Torrent.Models
{
    public class AnnounceResponse
    {
        private const int PeerIpByteCount = 6;

        public string ErrorMessage { get; set; }

        public string WarningMessage { get; set; }

        //in seconds
        public int Interval { get; set; }

        public int MinInterval { get; set; }

        public string TrackerId { get; set; }

        public int Complete { get; set; }

        public int Incomplete { get; set; }

        public List<PeerResponseItem> Peers { get; set; }

        public AnnounceResponse(TorrentDataTrie trie)
        {
            this.ErrorMessage = trie.GetItemString("failure reason");
            this.Peers = new List<PeerResponseItem>();

            if(!string.IsNullOrWhiteSpace(this.ErrorMessage))
                return;

            this.WarningMessage = trie.GetItemString("warning message");
            this.Interval = trie.GetItemInteger("interval") != null ? trie.GetItemInteger("interval").Value : 0;
            this.MinInterval = trie.GetItemInteger("min interval") != null ? trie.GetItemInteger("min interval").Value : 0;
            this.TrackerId = trie.GetItemString("tracker id");
            this.Complete = trie.GetItemInteger("complete") != null ? trie.GetItemInteger("complete").Value : 0;
            this.Incomplete = trie.GetItemInteger("incomplete") != null ? trie.GetItemInteger("incomplete").Value : 0;

            var peersStruct = trie.GetItem("peers");

            if (peersStruct.Type == TorrentMetaType.Unset)
                return;
            
            if (peersStruct.Type == TorrentMetaType.String)
            {
                this.Peers = ExtractPeers(peersStruct.BencodeByteData);
                return;
            }

            var items = trie.GetChildrenByType(TorrentMetaType.Dictionary, peersStruct);

            foreach (var item in items)
            {
                var peer = new PeerResponseItem();

                for (var i = 0; i < item.Children.Count; ++i)
                {
                    if (item.Children[i].Value == "port")
                    {
                        int.TryParse(item.Children[i + 1].Value, out var port);
                        peer.Port = port;
                    }
                    else if (item.Children[i].Value == "peer id")
                    {
                        peer.PeerId = item.Children[i + 1].Value;
                    }
                    else if (item.Children[i].Value == "ip")
                    {
                        peer.PeerIp = item.Children[i + 1].Value;
                    }

                    ++i;
                }

                this.Peers.Add(peer);
            }
        }

        private bool VerifyPeerIpList(List<byte> peerIps)
        {
            //the peers value may be a string consisting of multiples of 6 bytes.
            //First 4 bytes are the IP address and last 2 bytes are the port number.
            //All in network (big endian) notation.
            return peerIps.Count > 0 && peerIps.Count % PeerIpByteCount == 0;
        }

        private List<PeerResponseItem> ExtractPeers(List<byte> bencodeByteData)
        {
            var result = new List<PeerResponseItem>();

            if (!VerifyPeerIpList(bencodeByteData))
                return result;

            for (var i = 0; i < bencodeByteData.Count / PeerIpByteCount; ++i)
            {
                var index = i * PeerIpByteCount;
                var peer = new PeerResponseItem
                {
                    PeerId = string.Empty,
                    PeerIp = (bencodeByteData[index] & 0xFF) + "." +
                             (bencodeByteData[index + 1] & 0xFF) + "." + 
                             (bencodeByteData[index + 2] & 0xFF) + "." +
                             (bencodeByteData[index + 3] & 0xFF),
                    Port = BitConverter.ToUInt16(new byte[2] { bencodeByteData[index + 5], bencodeByteData[index + 4] }, 0)
                };

                result.Add(peer);
            }

            return result;
        }
    }
}
