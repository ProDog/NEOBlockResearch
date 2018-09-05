using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using ThinNeo;

namespace SmartContractDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("NEO SmartContract Test!");
            Console.WriteLine("Please input your wif:");
            var wif = Console.ReadLine();

            PubScDemo(wif);

        }

        private static void PubScDemo(string wif)
        {
            string assetid = "0x602c79718b16e442de58778e148d0b1084e3b2dffd5de6b7b16cee7969282de7";
            string api = "https://api.nel.group/api/testnet";
            byte[] prikey = ThinNeo.Helper.GetPrivateKeyFromWIF(wif);
            byte[] pubkey = ThinNeo.Helper.GetPublicKeyFromPrivateKey(prikey);
            string address = ThinNeo.Helper.GetAddressFromPublicKey(pubkey);

            Dictionary<string, List<Utxo>> dir = GetBalanceByAddress(api, address);

            //从文件中读取合约脚本
            byte[] script = System.IO.File.ReadAllBytes("NeoContracts.avm"); //这里填你的合约所在地址
            Console.WriteLine("合约脚本:" + ThinNeo.Helper.Bytes2HexString(script));
            Console.WriteLine("合约脚本hash：" + ThinNeo.Helper.Bytes2HexString(ThinNeo.Helper.GetScriptHashFromScript(script).data.ToArray().Reverse().ToArray()));
            byte[] parameter__list = ThinNeo.Helper.HexString2Bytes("0710");  //这里填合约入参  例：0610代表（string，[]）
            byte[] return_type = ThinNeo.Helper.HexString2Bytes("05");  //这里填合约的出参
            int need_storage = 0;
            int need_nep4 = 0;
            int need_canCharge = 0;
            string name = "tgas";
            string version = "1.0";
            string auther = "Zhang";
            string email = "0";
            string description = "0";
            using (ThinNeo.ScriptBuilder sb = new ThinNeo.ScriptBuilder())
            {
                var ss = need_storage  ;
                //倒叙插入数据
                sb.EmitPushString(description);
                sb.EmitPushString(email);
                sb.EmitPushString(auther);
                sb.EmitPushString(version);
                sb.EmitPushString(name);
                sb.EmitPushNumber(need_storage | need_nep4|need_canCharge);
                sb.EmitPushBytes(return_type);
                sb.EmitPushBytes(parameter__list);
                sb.EmitPushBytes(script);
                sb.EmitSysCall("Neo.Contract.Create");

                string scriptPublish = ThinNeo.Helper.Bytes2HexString(sb.ToArray());
                //用ivokescript试运行并得到消耗

                byte[] postdata;
                var url = MakeRpcUrlPost(api, "invokescript", out postdata, new MyJson.JsonNode_ValueString(scriptPublish));
                var result = HttpPost(url, postdata);
                //string result = http.Post(api, "invokescript", new MyJson.JsonNode_Array() { new MyJson.JsonNode_ValueString(scriptPublish) },Encoding.UTF8);
                var consume = (((MyJson.Parse(result) as MyJson.JsonNode_Object)["result"] as MyJson.JsonNode_Array)[0] as MyJson.JsonNode_Object)["gas_consumed"].ToString();
                decimal gas_consumed = decimal.Parse(consume);
                ThinNeo.InvokeTransData extdata = new ThinNeo.InvokeTransData();
                extdata.script = sb.ToArray();

                //Console.WriteLine(ThinNeo.Helper.Bytes2HexString(extdata.script));
                extdata.gas = Math.Ceiling(gas_consumed > 10 ? gas_consumed - 10 : gas_consumed);

                //拼装交易体
                ThinNeo.Transaction tran = MakeTran(dir, null, new ThinNeo.Hash256(assetid), extdata.gas);
                tran.version = 1;
                tran.extdata = extdata;
                tran.type = ThinNeo.TransactionType.InvocationTransaction;
                byte[] msg = tran.GetMessage();
                byte[] signdata = ThinNeo.Helper.Sign(msg, prikey);
                tran.AddWitness(signdata, pubkey, address);
                string txid = tran.GetHash().ToString();
                byte[] data = tran.GetRawData();
                string rawdata = ThinNeo.Helper.Bytes2HexString(data);

                //Console.WriteLine("scripthash:"+scripthash);

                url = MakeRpcUrlPost(api, "sendrawtransaction", out postdata, new MyJson.JsonNode_ValueString(rawdata));
                result = HttpPost(url, postdata);

                MyJson.JsonNode_Object resJO = (MyJson.JsonNode_Object)MyJson.Parse(result);
                Console.WriteLine(resJO.ToString());
            }



            ////byte[] prikey = ThinNeo.Helper.GetPrivateKeyFromWIF(wif);
            //byte[] pubkey = ThinNeo.Helper.GetPublicKeyFromPrivateKey(prikey);
            //string address = ThinNeo.Helper.GetAddressFromPublicKey(pubkey);

            //Dictionary<string, List<UTXO>> dic_UTXO = new Dictionary<string, List<UTXO>>();

            ////读取合约avm文件
            //byte[] script = System.IO.File.ReadAllBytes("NeoContracts.avm");
            //Console.WriteLine("合约脚本：" + ThinNeo.Helper.Bytes2HexString(script));
            //Console.WriteLine("合约脚本哈希:" + ThinNeo.Helper.Bytes2HexString(ThinNeo.Helper.GetScriptHashFromScript(script)
            //                      .data.ToArray().Reverse().ToArray()));
            //byte[] parameter__list = ThinNeo.Helper.HexString2Bytes("0710");//合约入参
            //byte[] return_type = ThinNeo.Helper.HexString2Bytes("05");//合约出参
            //int need_storage = 1;
            ////int need_nep4 = 0;
            ////int need_canCharge = 4;
            //string name = "sgas";
            //string version = "1.0";
            //string auther = "NEL";
            //string email = "0";
            //string description = "0";
            //using (ThinNeo.ScriptBuilder sb = new ThinNeo.ScriptBuilder())
            //{
            //    var ss = need_storage    ;

            //    sb.EmitPushString(description);
            //    sb.EmitPushString(email);
            //    sb.EmitPushString(auther);
            //    sb.EmitPushString(version);
            //    sb.EmitPushString(name);
            //    sb.EmitPushNumber(need_storage    );
            //    sb.EmitPushBytes(return_type);
            //    sb.EmitPushBytes(parameter__list);
            //    sb.EmitPushBytes(script);
            //    sb.EmitSysCall("Neo.Contract.Create");

            //    string scriptPublish = ThinNeo.Helper.Bytes2HexString(sb.ToArray());

            //    byte[] postdata;
            //    var method = "invokescript";
            //    var url = Helper.MakeRpcUrlPost(api, method, out postdata, new MyJson.JsonNode_ValueString(scriptPublish));
            //    var result = await Helper.HttpPost(url, postdata);

            //    var consume =
            //        (((MyJson.Parse(result) as MyJson.JsonNode_Object)["result"] as MyJson.JsonNode_Array)[0] as
            //            MyJson.JsonNode_Object)["gas_consumed"].ToString();
            //    decimal gas_consume = decimal.Parse(consume);
            //    ThinNeo.InvokeTransData extdata = new InvokeTransData();
            //    extdata.script = sb.ToArray();
            //    extdata.gas = Math.Ceiling(gas_consume - 10);

            //    ThinNeo.Transaction tran = MakeTran(dic_UTXO, null, new ThinNeo.Hash256(assetid), extdata.gas);

            //    tran.version = 1;
            //    tran.extdata = extdata;
            //    tran.type = ThinNeo.TransactionType.InvocationTransaction;
            //    byte[] msg = tran.GetMessage();
            //    byte[] signdata = ThinNeo.Helper.Sign(msg, prikey);
            //    tran.AddWitness(signdata, pubkey, address);
            //    string txid = tran.GetHash().ToString();
            //    byte[] data = tran.GetRawData();
            //    string rawdata = ThinNeo.Helper.Bytes2HexString(data);

            //    url = Helper.MakeRpcUrlPost(api, "sendrawtransaction", out postdata,
            //        new MyJson.JsonNode_ValueString(rawdata));
            //    result = await Helper.HttpPost(url, postdata);
            //    MyJson.JsonNode_Object resJO = (MyJson.JsonNode_Object) MyJson.Parse(result);
            //    Console.WriteLine(resJO.ToString());
            //}
        }

        private static Transaction MakeTran(Dictionary<string, List<Utxo>> dic_UTXO, string targetAddr, Hash256 assetid, decimal sendCount)
        {
            if(!dic_UTXO.ContainsKey(assetid.ToString()))
                throw new Exception("No Money!");
            List<Utxo> utxos = dic_UTXO[assetid.ToString()];
            var tran = new ThinNeo.Transaction();
            tran.type = ThinNeo.TransactionType.ContractTransaction;
            tran.version = 0;
            tran.extdata = null;
            tran.attributes = new ThinNeo.Attribute[0];
            var scraddr = "";

            utxos.Sort((a, b) =>
            {
                if (a.value > b.value)
                    return 1;
                else if (a.value < b.value)
                    return -1;
                else
                    return 0;
            });

            decimal count = decimal.Zero;
            List<ThinNeo.TransactionInput> list_inputs = new List<TransactionInput>();
            for (int i = 0; i < utxos.Count; i++)
            {
                ThinNeo.TransactionInput input = new TransactionInput();
                input.hash = utxos[i].txid;
                input.index = (ushort) utxos[i].n;
                list_inputs.Add(input);
                count += utxos[i].value;
                scraddr = utxos[i].addr;
                if (count >= sendCount)
                    break;
            }
            tran.inputs = list_inputs.ToArray();
            if (count >= sendCount)
            {
                List<ThinNeo.TransactionOutput> list_outputs = new List<TransactionOutput>();
                if (sendCount > decimal.Zero && targetAddr != null)
                {
                    ThinNeo.TransactionOutput output = new TransactionOutput();
                    output.assetId = assetid;
                    output.value = sendCount;
                    output.toAddress = ThinNeo.Helper.GetPublicKeyHashFromAddress(targetAddr);
                    list_outputs.Add(output);
                }

                var change = count - sendCount;
                if (change > decimal.Zero)
                {
                    ThinNeo.TransactionOutput outputchange = new TransactionOutput();
                    outputchange.assetId = assetid;
                    outputchange.toAddress = ThinNeo.Helper.GetPublicKeyHashFromAddress(scraddr);
                    outputchange.value = change;
                    list_outputs.Add(outputchange);
                }

                tran.outputs = list_outputs.ToArray();
            }
            else
            {
                throw  new Exception("no enough money!");
            }

            return tran;
        }

        //获取地址的utxo来得出地址的资产  
        private static  Dictionary<string, List<Utxo>> GetBalanceByAddress(string api, string _addr)
        {
            MyJson.JsonNode_Object response = (MyJson.JsonNode_Object)MyJson.Parse(HttpGet("https://api.nel.group/api/testnet?method=getutxo&id=1&params=['" + _addr + "']"));
            MyJson.JsonNode_Array resJA = (MyJson.JsonNode_Array)response["result"];
            Dictionary<string, List<Utxo>> _dir = new Dictionary<string, List<Utxo>>();
            foreach (MyJson.JsonNode_Object j in resJA)
            {
                Utxo utxo = new Utxo(j["addr"].ToString(), new ThinNeo.Hash256(j["txid"].ToString()), j["asset"].ToString(), decimal.Parse(j["value"].ToString()), int.Parse(j["n"].ToString()));
                if (_dir.ContainsKey(j["asset"].ToString()))
                {
                    _dir[j["asset"].ToString()].Add(utxo);
                }
                else
                {
                    List<Utxo> l = new List<Utxo>();
                    l.Add(utxo);
                    _dir[j["asset"].ToString()] = l;
                }

            }
            return _dir;
        }

        private static string MakeRpcUrlPost(string url, string method, out byte[] data, params MyJson.IJsonNode[] _params)
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

        private static  string HttpGet(string url)
        {
            WebClient wc = new WebClient();
            return  wc.DownloadString(url);
        }
        private static  string HttpPost(string url, byte[] data)
        {
            WebClient wc = new WebClient();
            wc.Headers["content-type"] = "text/plain;charset=UTF-8";
            byte[] retdata =  wc.UploadData(url, "POST", data);
            return System.Text.Encoding.UTF8.GetString(retdata);
        }
    }
}
