﻿using System.Collections.Generic;

namespace KosTorrentCli.Torrent
{
    public class TorrentFilePieceInfo
    {
        public int Length { get; set; }

        public string MdSum { get; set; }

        public List<string> Path { get; set; }
    }
}
