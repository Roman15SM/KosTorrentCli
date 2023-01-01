using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using KosTorrentCli.Bencode;
using KosTorrentCli.Torrent.Models;

namespace KosTorrentCli.Torrent
{
    public class Processor
    {
        private object _locker = new object();

        /// <summary>
        /// Get peers in parallel from AnnounceUrl and all urls specified in AnnounceList.
        /// Urls in Announce List are reserves in case if Announce Url is not working + there are also chance to collect extra peers.
        /// All duplicates will be eliminated by HashSet as it is.
        /// </summary>
        /// <param name="metaInfo"></param>
        /// <param name="infoHash"></param>
        /// <returns></returns>
        public List<PeerResponseItem> GetPeers(TorrentMetaInfo metaInfo, byte[] infoHash, string peerId)
        {
            var urlList = new List<string>(metaInfo.AnnounceList)
            {
                metaInfo.AnnounceUrl
            };

            var encodedHash = HttpUtility.UrlEncode(infoHash);
            var peers = new List<PeerResponseItem>();
            var strPeers = new HashSet<string>();

            Parallel.ForEach(urlList, ul =>
            {
                var responsePeers = GetPeersFromUrl(metaInfo, ul, encodedHash, peerId);

                lock (_locker)
                {
                    foreach(var peer in responsePeers)
                    {
                        if (IsIpValid(peer.PeerIp) && strPeers.Add(peer.GetIp()))
                        {
                            peers.Add(peer);
                        }
                    }
                }
            });

            return peers;
        }

        /// <summary>
        /// Get peers ip addresses from url. Currently I'm not using compact version here
        /// TODO: implement compact way possibility
        /// </summary>
        /// <param name="metaInfo"></param>
        /// <param name="announceUrl"></param>
        /// <param name="hashedInfo"></param>
        /// <returns></returns>
        public List<PeerResponseItem> GetPeersFromUrl(TorrentMetaInfo metaInfo, string announceUrl, string hashedInfo, string peerId)
        {
            if (!IsUrlValid(announceUrl))
                return new List<PeerResponseItem>();
            var hasParameters = HttpUtility.ParseQueryString(metaInfo.AnnounceUrl).Count > 1;

            var url = $"{announceUrl}{(hasParameters ? "&" : "?")}info_hash={hashedInfo}&peer_id={peerId}" +
                      $"&port=6881&uploaded=0&downloaded=0&left={metaInfo.Info.TotalLength}&compact=0&no_peer_id=1&event=started";

            var encodedUrl = HttpUtility.UrlPathEncode(url);

            var request = (HttpWebRequest)WebRequest.Create(encodedUrl);
            var response = (HttpWebResponse)request.GetResponse();

            using var ms = new MemoryStream();
            response.GetResponseStream()?.CopyTo(ms);

            var parser = new Parser();
            var data = ms.ToArray();
            var responseText = Encoding.ASCII.GetString(data);
            var responseTrie = parser.Parse(responseText, data);
            var responseObj = new AnnounceResponse(responseTrie);

            return responseObj.Peers;
        }

        public byte[] GenerateSha1Hash(byte[] input)
        {
            using var hashManager = new SHA1Managed();
            var hash = hashManager.ComputeHash(input);

            return hash;
        }

        private bool IsUrlValid(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
                   && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }

        private bool IsIpValid(string ip)
        {
            if (!IPAddress.TryParse(ip, out var address)) 
                return false;

            return address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6;
        }
    }
}
