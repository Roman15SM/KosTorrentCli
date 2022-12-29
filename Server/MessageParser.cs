using System;
using System.Collections.Generic;
using System.Linq;

namespace KosTorrentCli.Server
{
    public static class MessageParser
    {
        public static bool ParseChoke(byte[] message)
        {
            var chokeRequest = MessageGenerator.GenerateChokeRequest();

            if (message.Length < chokeRequest.Length)
                return false;

            for (var i = 0; i < chokeRequest.Length; ++i)
            {
                if (message[i] != chokeRequest[i])
                    return false;
            }

            return true;
        }

        public static bool ParseUnChoke(byte[] message)
        {
            var chokeRequest = MessageGenerator.GenerateUnChokeRequest();

            if (message.Length < chokeRequest.Length)
                return false;

            for (var i = 0; i < chokeRequest.Length; ++i)
            {
                if (message[i] != chokeRequest[i])
                    return false;
            }

            return true;
        }

        public static bool ParsePiece(byte[] message, int responseLength)
        {
            var lengthArr = message.Take(4).Reverse().ToArray();
            var length = BitConverter.ToInt32(lengthArr);

            if (responseLength - 4 != length)
                return false;

            if (message[4] != 7)
                return false;

            return true;
        }

        public static int GetBlockLength(IEnumerable<byte> message, int offset)
        {
            var lengthArr = message.Skip(offset).Take(4).Reverse().ToArray();
            return BitConverter.ToInt32(lengthArr);
        }

        public static int GetPieceIndex(byte[] message)
        {
            var lengthArr = message.Skip(5).Take(4).Reverse().ToArray();
            return BitConverter.ToInt32(lengthArr);
        }

        public static byte[] GetBitFieldBody(byte[] message)
        {
            return message.Skip(5).ToArray();
        }

        public static bool ParseHave(byte[] message)
        {
            var haveRequest = MessageGenerator.GenerateHaveRequest(0);

            if (message.Length < haveRequest.Length)
                return false;

            for (var i = 0; i < haveRequest.Length - 4; ++i)
            {
                if (message[i] != haveRequest[i])
                    return false;
            }

            return true;
        }
    }
}
