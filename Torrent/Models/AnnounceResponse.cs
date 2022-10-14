using System.Collections.Generic;
using KosTorrentCli.Bencode;

namespace KosTorrentCli.Torrent.Models
{
    public class AnnounceResponse
    {
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
    }
}
