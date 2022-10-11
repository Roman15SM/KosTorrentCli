using System.Collections.Generic;

namespace KosTorrentCli.Bencode
{
    public class TorrentMetaItem
    {
        public TorrentMetaType Type { get; set; }

        public string Value { get; set; }

        public int StartPosition { get; set; }

        public List<byte> BencodeByteData { get; set; }

        public List<TorrentMetaItem> Children { get; set; }

        public TorrentMetaItem()
        {
            Children = new List<TorrentMetaItem>();
        }
    }
}
