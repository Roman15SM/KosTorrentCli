using KosTorrentCli.Torrent.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace KosTorrentCli.Torrent
{
    public class FileCreator
    {
        private TorrentFilePieceInfo[] _pieceLocationData;

        public void GenerateFolderStructure(TorrentMetaInfo torrentMetaData)
        {
            var currentFilePieces = torrentMetaData.Info.Pieces.Count > 0
                ? torrentMetaData.Info.Pieces
                : new List<TorrentFilePieceInfo>
                {
                    new TorrentFilePieceInfo
                    {
                        Length = (long)(torrentMetaData.Info.PiecesBytes.Count/20) * torrentMetaData.Info.PieceLength,
                        Path = new List<string>
                        {
                            torrentMetaData.Info.Name
                        }
                    }
                };

            //folders creation
            foreach (var piece in currentFilePieces)
            {
                if (piece.Path.Count <= 1)
                {
                    Directory.CreateDirectory(torrentMetaData.Info.Name);
                    piece.Path[0] = torrentMetaData.Info.Name + "/" + piece.Path[0];
                    continue;
                }

                var piecePath = torrentMetaData.Info.Name + "/";

                for (var i = 0; i < piece.Path.Count - 1; ++i)
                {
                    piecePath += piece.Path[i] + "/";
                }

                Directory.CreateDirectory(piecePath);
                piece.Path[0] = piecePath + piece.Path.Last();
            }

            foreach (var piece in currentFilePieces)
            {
                using var fileStream = new FileStream(piece.Path[0], FileMode.OpenOrCreate, FileAccess.Write);
                fileStream.SetLength(piece.Length);
            }

            this._pieceLocationData = currentFilePieces.ToArray();
        }

        public void AllocatePiece(byte[] pieceData, TorrentMetaInfo metaInfo, int pieceNumber)
        {
            long pieceStartPosition = pieceNumber * metaInfo.Info.PieceLength;
            long currentPrefixSize = 0;

            foreach (var pieceInfo in this._pieceLocationData)
            {
                if (pieceStartPosition >= currentPrefixSize + pieceInfo.Length)
                {
                    currentPrefixSize += pieceInfo.Length;
                    continue;
                }
                
                if(currentPrefixSize > pieceStartPosition + pieceData.Length)
                    return;

                this.PopulatePieceDataInFile(pieceData, pieceInfo, pieceStartPosition, currentPrefixSize);
                currentPrefixSize += pieceInfo.Length;
            }
        }

        private void PopulatePieceDataInFile(byte[] pieceData, TorrentFilePieceInfo pieceInfo, long pieceStartPosition, long currentPrefixSize)
        {
            using (Stream stream = File.Open(pieceInfo.Path[0], FileMode.Open))
            {
                long position = 0;
                long offset = 0;

                if (pieceStartPosition < currentPrefixSize)
                    offset = currentPrefixSize - pieceStartPosition;

                if (pieceStartPosition > currentPrefixSize)
                    position = pieceStartPosition - currentPrefixSize;

                stream.Position = position;
                stream.Write(pieceData, (int)offset, pieceData.Length - (int)offset);
            }
        }
    }
}
