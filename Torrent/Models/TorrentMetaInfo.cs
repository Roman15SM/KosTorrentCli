using System;
using System.Collections.Generic;
using KosTorrentCli.Bencode;

namespace KosTorrentCli.Torrent.Models
{
    public class TorrentMetaInfo
    {
        public string AnnounceUrl { get; set; }

        public List<string> AnnounceList { get; set; }

        public DateTime? CreationDate { get; set; }

        public string Comment { get; set; }

        public string CreatedBy { get; set; }

        public string Encoding { get; set; }

        public TorrentStructureInfo Info { get; set; }

        public TorrentMetaInfo(TorrentDataTrie trie)
        {
            this.AnnounceUrl = trie.GetItemString("announce");
            this.AnnounceList = trie.GetItemStringList("announce-list");
            this.CreationDate = trie.GetDate("creation date");
            this.Comment = trie.GetItemString("comment");
            this.CreatedBy = trie.GetItemString("created by");
            this.Encoding = trie.GetItemString("encoding");
            this.Info = new TorrentStructureInfo
            {
                Name = trie.GetItemString("name"),
                IsPrivate = trie.GetItemString("private") == "1",
                PieceLength = trie.GetItemInteger("piece length") != null ? trie.GetItemInteger("piece length").Value : 0,
                Pieces = new List<TorrentFilePieceInfo>(),
                BencodeByteData = trie.GetItem("info")?.BencodeByteData,
                PiecesBytes = trie.GetItem("pieces")?.BencodeByteData,
            };

            var pieceStruct = trie.GetItem("files");

            if (pieceStruct.Type == TorrentMetaType.Unset)
                return;

            var items = trie.GetChildrenByType(TorrentMetaType.Dictionary, pieceStruct);

            foreach (var item in items)
            {
                var piece = new TorrentFilePieceInfo
                {
                    Path = new List<string>()
                };

                for (var iter = 0; iter < item.Children.Count; ++iter)
                {
                    if (item.Children[iter].Value == "length")
                    {
                        int.TryParse(item.Children[iter + 1].Value, out var length);
                        piece.Length = length;
                        this.Info.TotalLength += length;
                    }
                    else if (item.Children[iter].Value == "md5sum")
                    {
                        piece.MdSum = item.Children[iter + 1].Value;
                    }
                    else if (item.Children[iter].Value == "path")
                    {
                        foreach (var pathPiece in item.Children[iter + 1].Children)
                        {
                            if(pathPiece.Type == TorrentMetaType.String)
                                piece.Path.Add(pathPiece.Value);
                        }
                    }

                    ++iter;
                }

                this.Info.Pieces.Add(piece);
            }
        }
    }
}
