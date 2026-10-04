using KosTorrentCli.Torrent.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

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
                        Length = torrentMetaData.Info.TotalLength,
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
            //cast before multiplication, otherwise int overflows for torrents larger than 2GB
            long pieceStartPosition = (long)pieceNumber * metaInfo.Info.PieceLength;
            long currentPrefixSize = 0;

            foreach (var pieceInfo in this._pieceLocationData)
            {
                if (pieceStartPosition >= currentPrefixSize + pieceInfo.Length)
                {
                    currentPrefixSize += pieceInfo.Length;
                    continue;
                }

                if(currentPrefixSize >= pieceStartPosition + pieceData.Length)
                    return;

                this.PopulatePieceDataInFile(pieceData, pieceInfo, pieceStartPosition, currentPrefixSize);
                currentPrefixSize += pieceInfo.Length;
            }
        }

        private void PopulatePieceDataInFile(byte[] pieceData, TorrentFilePieceInfo pieceInfo, long pieceStartPosition, long currentPrefixSize)
        {
            //files are shared for read/write, so antivirus or explorer reading the file does not block piece writing
            using (Stream stream = OpenWithRetry(pieceInfo.Path[0]))
            {
                long position = 0;
                long offset = 0;

                if (pieceStartPosition < currentPrefixSize)
                    offset = currentPrefixSize - pieceStartPosition;

                if (pieceStartPosition > currentPrefixSize)
                    position = pieceStartPosition - currentPrefixSize;

                //piece can continue in the next file, so write only the part which fits into the current one
                var count = Math.Min(pieceData.Length - offset, pieceInfo.Length - position);

                stream.Position = position;
                stream.Write(pieceData, (int)offset, (int)count);
            }
        }

        /// <summary>
        /// Some processes (e.g. antivirus scanning a freshly written .exe) can lock the file for a short time.
        /// </summary>
        private Stream OpenWithRetry(string path)
        {
            const int attempts = 5;
            const int delayMs = 500;

            for (var attempt = 1; ; ++attempt)
            {
                try
                {
                    return new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
                }
                catch (IOException e) when (attempt < attempts && e is not FileNotFoundException && e is not DirectoryNotFoundException)
                {
                    Log.Warning($"File {path} is locked, retry {attempt}/{attempts - 1}: {e.Message}");
                    Thread.Sleep(delayMs);
                }
            }
        }
    }
}
