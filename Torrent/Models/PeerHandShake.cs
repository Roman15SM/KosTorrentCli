using System.Collections.Generic;
using System.Text;

namespace KosTorrentCli.Torrent.Models
{
    public class PeerHandShake
    {
        //pstr
        public const string ProtocolString = "BitTorrent protocol";

        public byte[] InfoHash { get; set; }

        public string PeerId { get; set; }

        public PeerHandShake(string peerId, byte[] infoHash)
        {
            PeerId = peerId;
            InfoHash = infoHash;
        }

        //handshake: <pstrlen><pstr><reserved><info_hash><peer_id>
        public byte[] GenerateHandShakeMessage()
        {
            var handshakeMessage = new List<byte>();

            handshakeMessage.AddRange(Encoding.ASCII.GetBytes(new[] { '\x13' }));
            var protocolPart = Encoding.ASCII.GetBytes($"{ProtocolString}");
            handshakeMessage.AddRange(protocolPart);

            //Supported version is 1.0 that is why all reserved 8 bytes are zeros
            var reservedPart = Encoding.ASCII.GetBytes(new[] { '\x00', '\x00', '\x00', '\x00', '\x00', '\x00', '\x00', '\x00' });
            handshakeMessage.AddRange(reservedPart);

            handshakeMessage.AddRange(InfoHash);

            var peerPart = Encoding.ASCII.GetBytes($"{PeerId}");
            handshakeMessage.AddRange(peerPart);

            return handshakeMessage.ToArray();
        }
    }
}
