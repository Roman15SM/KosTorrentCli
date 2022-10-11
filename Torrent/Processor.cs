using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace KosTorrentCli.Torrent
{
    public class Processor
    {
        public void GetPeers(TorrentMetaInfo metaInfo)
        {
            var hashedInfo = GenerateSha1Hash(metaInfo.Info.BencodeByteData.ToArray());
            var hasParameters = HttpUtility.ParseQueryString(metaInfo.AnnounceUrl).Count > 1;

            var url = $"{metaInfo.AnnounceUrl}{(hasParameters ? "&" : "?")}info_hash={hashedInfo}&peer_id={PeerIdGenerator.GetPeerId()}" +
                      $"&port=6881&uploaded=0&downloaded=0&left={metaInfo.Info.TotalLength}&compact=0&no_peer_id=1&event=started";

            var encodedUrl = HttpUtility.UrlPathEncode(url);

            var request = (HttpWebRequest)WebRequest.Create(encodedUrl);
            var response = (HttpWebResponse)request.GetResponse();

            using var ms = new MemoryStream();
            response.GetResponseStream()?.CopyTo(ms);
            var data = ms.ToArray();
            var responseText = Encoding.ASCII.GetString(data);
        }

        private string GenerateSha1Hash(byte[] input)
        {
            using var hashManager = new SHA1Managed();
            var hash = hashManager.ComputeHash(input);

            var encodedHash = HttpUtility.UrlEncode(hash);

            return encodedHash;
        }
    }
}
