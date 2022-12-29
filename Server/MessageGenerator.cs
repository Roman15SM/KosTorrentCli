using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KosTorrentCli.Server
{
    public static class MessageGenerator
    {
        public static byte[] GenerateBitFieldRequest()
        {
            throw new NotImplementedException();
        }

        public static byte[] GenerateKeepAliveRequest()
        {
            var keepAliveMessage = new List<byte>();

            //length part
            keepAliveMessage.AddRange(Encoding.ASCII.GetBytes(new[] { '\x00', '\x00', '\x00', '\x00' }));

            return keepAliveMessage.ToArray();
        }

        public static byte[] GenerateInterestedRequest()
        {
            var handshakeMessage = new List<byte>();

            //length part
            handshakeMessage.AddRange(Encoding.ASCII.GetBytes(new[] { '\x00', '\x00', '\x00', '\x01' }));

            //interested id = 2
            var idPart = Encoding.ASCII.GetBytes(new[] { '\x02' });
            handshakeMessage.AddRange(idPart);

            return handshakeMessage.ToArray();
        }

        public static byte[] GenerateChokeRequest()
        {
            var handshakeMessage = new List<byte>();

            //length part
            handshakeMessage.AddRange(Encoding.ASCII.GetBytes(new[] { '\x00', '\x00', '\x00', '\x01' }));

            //choke id = 0
            var idPart = Encoding.ASCII.GetBytes(new[] { '\x00' });
            handshakeMessage.AddRange(idPart);

            return handshakeMessage.ToArray();
        }

        public static byte[] GenerateUnChokeRequest()
        {
            var handshakeMessage = new List<byte>();

            //length part
            handshakeMessage.AddRange(Encoding.ASCII.GetBytes(new[] { '\x00', '\x00', '\x00', '\x01' }));

            //unChoke id = 1
            var idPart = Encoding.ASCII.GetBytes(new[] { '\x01' });
            handshakeMessage.AddRange(idPart);

            return handshakeMessage.ToArray();
        }

        public static byte[] GenerateHaveRequest(int index)
        {
            var handshakeMessage = new List<byte>();

            //length part
            handshakeMessage.AddRange(Encoding.ASCII.GetBytes(new[] { '\x00', '\x00', '\x00', '\x05' }));

            //have id = 4
            var idPart = Encoding.ASCII.GetBytes(new[] { '\x04' });
            handshakeMessage.AddRange(idPart);

            handshakeMessage.AddRange(BitConverter.GetBytes(index));

            return handshakeMessage.ToArray();
        }

        public static byte[] GenerateRequestRequest(int index, int begin, int length)
        {
            var handshakeMessage = new List<byte>();

            //length part
            //handshakeMessage.AddRange(Encoding.ASCII.GetBytes(new[] { '\x00', '\x00', '\x01', '\x03' }));
            uint mesLength = 13;
            handshakeMessage.AddRange(BitConverter.GetBytes(mesLength).Reverse());

            //request id = 6
            var idPart = Encoding.ASCII.GetBytes(new[] { '\x06' });
            handshakeMessage.AddRange(idPart);

            var indexPart = BitConverter.GetBytes(index).Reverse();
            handshakeMessage.AddRange(indexPart);

            var beginPart = BitConverter.GetBytes(begin).Reverse();
            handshakeMessage.AddRange(beginPart);

            var lengthPart = BitConverter.GetBytes(length).Reverse();
            handshakeMessage.AddRange(lengthPart);

            return handshakeMessage.ToArray();
        }
    }
}
