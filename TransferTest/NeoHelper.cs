using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ThinNeo;
using ThinNeo.Cryptography.Cryptography;

namespace TransferTest
{
    public class NeoHelper
    {
        public static byte[] GetPublicKeyFromPrivateKey(byte[] privateKey)
        {
            var PublicKey = ThinNeo.Cryptography.ECC.ECCurve.Secp256r1.G * privateKey;
            return PublicKey.EncodePoint(true);
        }

        public static string GetAddressFromPublicKey(byte[] publickey)
        {
            byte[] scriptHash = GetScriptHashFromPublicKey(publickey);
            return GetAddressFromScriptHash(scriptHash);
        }

        public static string GetAddressFromScriptHash(Hash160 scripthash)
        {
            byte[] data = new byte[20 + 1];
            data[0] = 0x17;
            Array.Copy(scripthash, 0, data, 1, 20);
            var hash = sha256.ComputeHash(data);
            hash = sha256.ComputeHash(hash);

            var alldata = data.Concat(hash.Take(4)).ToArray();

            return Base58.Encode(alldata);
        }

        public static Hash160 GetScriptHashFromPublicKey(byte[] publicKey)
        {
            byte[] script = GetScriptFromPublicKey(publicKey);
            var scripthash = sha256.ComputeHash(script);
            scripthash = ripemd160.ComputeHash(scripthash);
            return scripthash;
        }

        public static byte[] GetScriptFromPublicKey(byte[] publicKey)
        {
            byte[] script = new byte[publicKey.Length + 2];
            script[0] = (byte) publicKey.Length;
            Array.Copy(publicKey, 0, script, 1, publicKey.Length);
            script[script.Length - 1] = 172; //CHECKSIG
            return script;
        }

        static System.Security.Cryptography.SHA256 sha256 = System.Security.Cryptography.SHA256.Create();
        static RIPEMD160Managed ripemd160 = new RIPEMD160Managed();

    }

    public class UTXO
    {
        public Hash256 txid;
        public int n;

        public string addr;
        public string asset;
        public decimal value;

        public UTXO(string _addr, Hash256 _txid, string _asset, decimal _value, int _n)
        {
            this.addr = _addr;
            this.asset = _asset;
            this.txid = _txid;
            this.n = _n;
            this.value = _value;
        }
    }

    public class HttpHelper
    {
        public static async Task<string> HttpGet(string url)
        {
            WebClient wc = new WebClient();
            return await wc.DownloadStringTaskAsync(url);
        }

        public static async Task<string> HttpPost(string url, byte[] data)
        {
            WebClient wc = new WebClient();
            wc.Headers["content-type"] = "text/plain;charset=UTF-8";
            byte[] retdata = await wc.UploadDataTaskAsync(url, "POST", data);
            return System.Text.Encoding.UTF8.GetString(retdata);
        }

        public static string MakeRpcUrlPost(string url, string method, out byte[] data,
            params MyJson.IJsonNode[] _params)
        {
            //if (url.Last() != '/')
            //    url = url + "/";
            var json = new MyJson.JsonNode_Object();
            json["id"] = new MyJson.JsonNode_ValueNumber(1);
            json["jsonrpc"] = new MyJson.JsonNode_ValueString("2.0");
            json["method"] = new MyJson.JsonNode_ValueString(method);
            StringBuilder sb = new StringBuilder();
            var array = new MyJson.JsonNode_Array();
            for (var i = 0; i < _params.Length; i++)
            {

                array.Add(_params[i]);
            }

            json["params"] = array;
            data = System.Text.Encoding.UTF8.GetBytes(json.ToString());
            return url;
        }

        public static string MakeRpcUrl(string url, string method, params MyJson.IJsonNode[] _params)
        {
            StringBuilder sb = new StringBuilder();
            if (url.Last() != '/')
                url = url + "/";

            sb.Append(url + "?jsonrpc=2.0&id=1&method=" + method + "&params=[");
            for (var i = 0; i < _params.Length; i++)
            {
                _params[i].ConvertToString(sb);
                if (i != _params.Length - 1)
                    sb.Append(",");
            }

            sb.Append("]");
            return sb.ToString();
        }

    }
}
