using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Threading.Tasks;
using ThinNeo;

namespace TransferTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Open Account:");
            //使用时填自己的WIF
            Console.WriteLine("Input your wif:");
            var wif = Console.ReadLine();
            
            byte[] prikey = Helper.GetPrivateKeyFromWIF(wif);
            byte[] pubkey = NeoHelper.GetPublicKeyFromPrivateKey(prikey);
            var address = NeoHelper.GetAddressFromPublicKey(pubkey);

            var targetAddr = "AGcnXDr6AxqtmVEK7vQZMbhLSRJaW7bZ8c";
            decimal sendCount = 2;

            //TransNEO(address, prikey, pubkey, targetAddr, sendCount);

            //TransNNC(address, prikey, pubkey, targetAddr, sendCount);

            //GetBalanceOfNNC(address);

            TransGasForNep5(address, prikey, pubkey, targetAddr, sendCount);

            Console.ReadKey();
        }

        private static async void TransGasForNep5(string address, byte[] prikey, byte[] pubkey, string targetAddr,
            decimal sendCount)
        {
            var assetnep5 = "0xa0b53d2efa8b1c4a62fcc1fcb54b7641510810c7";
            var assetgas = "0x602c79718b16e442de58778e148d0b1084e3b2dffd5de6b7b16cee7969282de7";
            string api = "https://api.nel.group/api/testnet";

            Dictionary<string, List<Utxo>> dir = await GetBalanceByAddress(api, address);
            ThinNeo.Transaction tran = null;
            {
                byte[] script = null;
                using (var sb = new ThinNeo.ScriptBuilder())
                {
                    var array = new MyJson.JsonNode_Array();
                    sb.EmitParamJson(array); //参数倒序入
                    sb.EmitParamJson(new MyJson.JsonNode_ValueString("(str)mintTokens")); //参数倒序入
                    ThinNeo.Hash160 shash = new ThinNeo.Hash160(assetnep5);
                    sb.EmitAppCall(shash); //nep5脚本
                    script = sb.ToArray();
                }

                var nep5scripthash = new ThinNeo.Hash160(assetnep5);
                var targetaddr = ThinNeo.Helper.GetAddressFromScriptHash(nep5scripthash);
                Console.WriteLine("contract address=" + targetaddr); //往合约地址转账

                //生成交易
                tran = makeTran(dir[assetgas], targetaddr, new ThinNeo.Hash256(assetgas), 5);
                tran.type = ThinNeo.TransactionType.InvocationTransaction;
                var idata = new ThinNeo.InvokeTransData();
                tran.extdata = idata;
                idata.script = script;
            }

            //sign and broadcast
            var signdata = ThinNeo.Helper.Sign(tran.GetMessage(), prikey);
            tran.AddWitness(signdata, pubkey, address);
            var trandata = tran.GetRawData();
            var strtrandata = ThinNeo.Helper.Bytes2HexString(trandata);
            byte[] postdata;
            var url = HttpHelper.MakeRpcUrlPost(api, "sendrawtransaction", out postdata, new MyJson.JsonNode_ValueString(strtrandata));
            var result = await HttpHelper.HttpPost(url, postdata);
            Console.WriteLine("得到的结果是：" + result);
            var json = MyJson.Parse(result).AsDict();
            if (json.ContainsKey("result"))
            {
                var resultv = json["result"].AsList()[0].AsDict();
                var txid = resultv["txid"].AsString();
                if (txid.Length > 0)
                {
                    Hash256 test = tran.GetHash();
                }

                Console.WriteLine("txid=" + txid);
            }
        }

        /// <summary>
        /// 调用合约中的balanceOf方法、
        /// </summary>
        /// <param name="address"></param>
        private static async void GetBalanceOfNNC(string address)
        {
            byte[] data = null;
            using (ScriptBuilder sb=new ScriptBuilder())
            {
                MyJson.JsonNode_Array array = new MyJson.JsonNode_Array();
                array.AddArrayValue("(addr)" + address);
                sb.EmitParamJson(array);
                sb.EmitPushString("balanceOf");
                sb.EmitAppCall(new Hash160("0xa0b53d2efa8b1c4a62fcc1fcb54b7641510810c7"));//合约脚本hash
                data = sb.ToArray();
            }

            string script = ThinNeo.Helper.Bytes2HexString(data);
            byte[] postdata;
            var url = HttpHelper.MakeRpcUrlPost("https://api.nel.group/api/testnet", "invokescript", out postdata,
                new MyJson.JsonNode_ValueString(script));
            var result = await HttpHelper.HttpPost(url, postdata);
            Console.WriteLine(result);
        }



        private static async void TransNEO(string address, byte[] prikey, byte[] pubkey, string targetAddr,decimal sendCount)
        {
            Dictionary<string, List<UTXO>> dic_UTXO = await GetUTXOByAddress(address);
            //0xc56f33fc6ecfcd0c225c4ab356fee59390af8560be0e930faebe74a6daff7c9b 是NEO资产类型
            Transaction tran = MakeTran(dic_UTXO, address, targetAddr, new Hash256("0xc56f33fc6ecfcd0c225c4ab356fee59390af8560be0e930faebe74a6daff7c9b"), sendCount);

            tran.version = 0;
            tran.attributes = new ThinNeo.Attribute[0];
            tran.type = ThinNeo.TransactionType.ContractTransaction;

            byte[] msg = tran.GetMessage();
            string msgstr = ThinNeo.Helper.Bytes2HexString(msg);
            byte[] signdata = ThinNeo.Helper.Sign(msg, prikey);
            tran.AddWitness(signdata,pubkey,address);
            string txid = tran.GetHash().ToString();
            byte[] data = tran.GetRawData();
            string rawdata = ThinNeo.Helper.Bytes2HexString(data);

            byte[] postdata;

            var url = HttpHelper.MakeRpcUrlPost("https://api.nel.group/api/testnet", "sendrawtransaction", out postdata,
                new MyJson.JsonNode_ValueString(rawdata));
            var result = await HttpHelper.HttpPost(url, postdata);
            MyJson.JsonNode_Object resJO = (MyJson.JsonNode_Object) MyJson.Parse(result);
            Console.WriteLine(resJO.ToString());

        }

       
        private static async void TransNNC(string address, byte[] prikey, byte[] pubkey, string targetAddr, decimal sendCount)
        {
            byte[] script = null;
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                var array = new MyJson.JsonNode_Array();
                array.AddArrayValue("(addr)" + address);
                array.AddArrayValue("(addr)" + targetAddr);
                array.AddArrayValue("(int)" + "1" + "00");
                sb.EmitParamJson(array);
                sb.EmitPushString("transfer");
                sb.EmitAppCall(new Hash160("0xbab964febd82c9629cc583596975f51811f25f47"));
                script = sb.ToArray();
            }

            Dictionary<string, List<UTXO>> dic_UTXO = await GetUTXOByAddress(address);
            Transaction tran = MakeTran(dic_UTXO, address, targetAddr,
                new ThinNeo.Hash256("0x602c79718b16e442de58778e148d0b1084e3b2dffd5de6b7b16cee7969282de7"), 0);
            tran.type = TransactionType.InvocationTransaction;
            tran.version = 0;
            tran.attributes=new ThinNeo.Attribute[0];
            var idata = new ThinNeo.InvokeTransData();
            tran.extdata = idata;
            idata.script = script;
            idata.gas = 0;

            byte[] msg = tran.GetMessage();
            string msgstr = ThinNeo.Helper.Bytes2HexString(msg);
            byte[] signdata = ThinNeo.Helper.Sign(msg, prikey);
            tran.AddWitness(signdata, pubkey, address);
            string txid = tran.GetHash().ToString();
            byte[] data = tran.GetRawData();
            string rawdata = ThinNeo.Helper.Bytes2HexString(data);

            byte[] postdata;
            var url = HttpHelper.MakeRpcUrlPost("https://api.nel.group/api/testnet", "sendrawtransaction", out postdata,
                new MyJson.JsonNode_ValueString(rawdata));
            var result = await HttpHelper.HttpPost(url, postdata);
            MyJson.JsonNode_Object resJO = (MyJson.JsonNode_Object) MyJson.Parse(result);
            Console.WriteLine(resJO.ToString());
        }


        private static Transaction MakeTran(Dictionary<string, List<UTXO>> dic_UTXO, string address, string targetAddr, Hash256 assetid, decimal sendCount)
        {
            if (!dic_UTXO.ContainsKey(assetid.ToString()))
                throw new Exception("no money!");

            List<UTXO> utxos = dic_UTXO[assetid.ToString()];
            Transaction tran = new Transaction();
            utxos.Sort((a, b) =>
            {
                if (a.value > b.value)
                    return 1;
                else if (a.value < b.value)
                    return -1;
                else
                {
                    return 0;
                }
            });

            decimal count = decimal.Zero;
            List<TransactionInput> list_inputs = new List<TransactionInput>();
            for (var i = 0; i < utxos.Count; i++)
            {
                TransactionInput input = new TransactionInput();
                input.hash = utxos[i].txid;
                input.index = (ushort)utxos[i].n;
                list_inputs.Add(input);
                count += utxos[i].value;
                if (count >= sendCount)
                {
                    break;
                }
            }

            tran.inputs = list_inputs.ToArray();

            if (count >= sendCount)
            {
                List<TransactionOutput> list_outputs = new List<TransactionOutput>();
                if (sendCount > decimal.Zero)
                {
                    TransactionOutput output = new TransactionOutput();
                    output.assetId = assetid;
                    output.value = sendCount;
                    output.toAddress = ThinNeo.Helper.GetPublicKeyHashFromAddress(targetAddr);
                    list_outputs.Add(output);
                }

                var change = count - sendCount;
                if (change > decimal.Zero)
                {
                    TransactionOutput outputchange = new TransactionOutput();
                    outputchange.toAddress = ThinNeo.Helper.GetPublicKeyHashFromAddress(address);
                    outputchange.value = change;
                    outputchange.assetId = assetid;
                    list_outputs.Add(outputchange);
                }

                tran.outputs = list_outputs.ToArray();
            }
            else
            {
                throw new Exception("No money!");
            }

            return tran;
        }

        private static async Task<Dictionary<string, List<UTXO>>> GetUTXOByAddress(string address)
        {
            MyJson.JsonNode_Object response = (MyJson.JsonNode_Object)MyJson.Parse(
                await HttpHelper.HttpGet("https://api.nel.group/api/testnet?method=getutxo&id=1&params=['" + address + "']"));
            MyJson.JsonNode_Array resJA = (MyJson.JsonNode_Array)response["result"];
            Dictionary<string, List<UTXO>> dic = new Dictionary<string, List<UTXO>>();
            foreach (MyJson.JsonNode_Object j in resJA)
            {
                UTXO utxo = new UTXO(j["addr"].ToString(), new Hash256(j["txid"].ToString()), j["asset"].ToString(),
                    decimal.Parse(j["value"].ToString()), int.Parse(j["n"].ToString()));
                if (dic.ContainsKey(j["asset"].ToString()))
                    dic[j["asset"].ToString()].Add(utxo);
                else
                {
                    List<UTXO> list = new List<UTXO>();
                    list.Add(utxo);
                    dic[j["asset"].ToString()] = list;
                }
            }
            return dic;
        }


        public static Transaction makeTran(List<Utxo> utxos, string targetaddr, ThinNeo.Hash256 assetid, decimal sendcount)
        {
            var tran = new ThinNeo.Transaction();
            tran.type = ThinNeo.TransactionType.ContractTransaction;
            tran.version = 0;//0 or 1
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
            List<ThinNeo.TransactionInput> list_inputs = new List<ThinNeo.TransactionInput>();
            for (var i = 0; i < utxos.Count; i++)
            {
                ThinNeo.TransactionInput input = new ThinNeo.TransactionInput();
                input.hash = utxos[i].txid;
                input.index = (ushort)utxos[i].n;
                list_inputs.Add(input);
                count += utxos[i].value;
                scraddr = utxos[i].addr;
                if (count >= sendcount)
                {
                    break;
                }
            }
            tran.inputs = list_inputs.ToArray();
            if (count >= sendcount)//输入大于等于输出
            {
                List<ThinNeo.TransactionOutput> list_outputs = new List<ThinNeo.TransactionOutput>();
                //输出
                if (sendcount > decimal.Zero && targetaddr != null)
                {
                    ThinNeo.TransactionOutput output = new ThinNeo.TransactionOutput();
                    output.assetId = assetid;
                    output.value = sendcount;
                    output.toAddress = ThinNeo.Helper.GetPublicKeyHashFromAddress(targetaddr);
                    list_outputs.Add(output);
                }

                //找零
                var change = count - sendcount;
                if (change > decimal.Zero)
                {
                    ThinNeo.TransactionOutput outputchange = new ThinNeo.TransactionOutput();
                    outputchange.toAddress = ThinNeo.Helper.GetPublicKeyHashFromAddress(scraddr);
                    outputchange.value = change;
                    outputchange.assetId = assetid;
                    list_outputs.Add(outputchange);

                }
                tran.outputs = list_outputs.ToArray();
            }
            else
            {
                throw new Exception("no enough money.");
            }
            return tran;
        }

        //获取地址的utxo来得出地址的资产  
        public static async Task<Dictionary<string, List<Utxo>>> GetBalanceByAddress(string api, string _addr)
        {
            MyJson.JsonNode_Object response = (MyJson.JsonNode_Object)MyJson.Parse(await HttpHelper.HttpGet(api + "?method=getutxo&id=1&params=['" + _addr + "']"));
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
    }
}
