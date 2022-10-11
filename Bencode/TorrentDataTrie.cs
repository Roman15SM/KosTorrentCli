using System;
using System.Collections.Generic;

namespace KosTorrentCli.Bencode
{
    public class TorrentDataTrie
    {
        private Dictionary<string, TorrentMetaItem> TrieItemLinks { get; set; }
        public List<TorrentMetaItem> Children { get; set; }

        public TorrentDataTrie()
        {
            TrieItemLinks = new Dictionary<string, TorrentMetaItem>();
            Children = new List<TorrentMetaItem>();
        }

        public TorrentMetaItem GetItem(string name)
        {
            if (TrieItemLinks.ContainsKey(name))
                return TrieItemLinks[name];

            return new TorrentMetaItem();
        }

        public DateTime? GetDate(string name)
        {
            var unixTimeStamp = this.GetItemInteger(name);

            if (unixTimeStamp == null)
                return null;

            var dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            return dateTime.AddSeconds(unixTimeStamp.Value).ToLocalTime();
        }

        public string GetItemString(string name)
        {
            if (TrieItemLinks.ContainsKey(name) && TrieItemLinks[name].Type == TorrentMetaType.String)
                return TrieItemLinks[name].Value;

            return string.Empty;
        }

        public int? GetItemInteger(string name)
        {
            if (!TrieItemLinks.ContainsKey(name) || TrieItemLinks[name].Type != TorrentMetaType.Integer) 
                return null;

            if (int.TryParse(TrieItemLinks[name].Value, out var intValue))
                return intValue;

            return null;
        }

        public List<TorrentMetaItem> GetChildrenByType(TorrentMetaType info, TorrentMetaItem parent)
        {
            var result = new List<TorrentMetaItem>();

            //TODO: Create a generic traversal method and remove similar traversal in GetItemStringList
            var queue = new Queue<TorrentMetaItem>();
            queue.Enqueue(parent);

            while (queue.Count > 0)
            {
                var count = queue.Count;

                for (var i = 0; i < count; ++i)
                {
                    var item = queue.Dequeue();

                    foreach (var child in item.Children)
                    {
                        queue.Enqueue(child);
                    }

                    if (item.Type == info)
                    {
                        result.Add(item);
                    }
                }
            }

            return result;
        }
        
        public List<string> GetItemStringList(string name)
        {
            var result = new List<string>();

            if (!TrieItemLinks.ContainsKey(name) || TrieItemLinks[name].Type != TorrentMetaType.List)
                return result;

            var queue = new Queue<TorrentMetaItem>();
            queue.Enqueue(TrieItemLinks[name]);

            //Iterative trie traversal via queue
            while (queue.Count > 0)
            {
                var count = queue.Count;

                for (var i = 0; i < count; ++i)
                {
                    var item = queue.Dequeue();

                    foreach (var child in item.Children)
                    {
                        queue.Enqueue(child);
                    }

                    if (item.Type == TorrentMetaType.String)
                    {
                        result.Add(item.Value);
                    }
                }
            }

            return result;
        }

        public void AddItem(string name, TorrentMetaItem item)
        {
            TrieItemLinks[name] = item;
        }
    }
}
