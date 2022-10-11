using System;
using System.IO;

namespace KosTorrentCli.Torrent
{
    public static class PeerIdGenerator
    {
        public static string GetPeerId()
        {
            var localPeer = "";
            var path = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "peer";

            if (File.Exists(path))
            {
                localPeer = File.ReadAllText(path);
            }

            if (localPeer.Length == 20)
                return localPeer;

            localPeer = Generate();
            File.WriteAllText(path, localPeer);

            return localPeer;
        }
        private static string Generate()
        {
            var peerId = "-RK-0001-";

            var rand = new Random();
            peerId += rand.Next(int.MaxValue).ToString().PadLeft(11, '0');
            
            return peerId;
        }
    }
}
