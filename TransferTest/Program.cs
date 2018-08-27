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
            //这是一个空密钥、使用时填自己的WIF
            var wif = "Kwky7bJRLzNzKbq2ZBFZp68pwhz2QKsG6HYamDGBSxKmGftw7YiQ";
            
            byte[] prikey = Helper.GetPrivateKeyFromWIF(wif);
            byte[] pubkey = NeoHelper.GetPublicKeyFromPrivateKey(prikey);
            var address = NeoHelper.GetAddressFromPublicKey(pubkey);

            var targetAddr = "AGcnXDr6AxqtmVEK7vQZMbhLSRJaW7bZ8c";
            decimal sendCount = 2;

            TransNEO(address, prikey, pubkey, targetAddr, sendCount);

            TransNNC(address, prikey, pubkey, targetAddr, sendCount);

            GetBalanceOfNNC(address);

            Console.ReadKey();
        }

        private static async void GetBalanceOfNNC(string address)
        {
            byte[] data = null;
            using (ScriptBuilder sb=new ScriptBuilder())
            {
                MyJson.JsonNode_Array array = new MyJson.JsonNode_Array();
                array.AddArrayValue("(addr)" + address);
                sb.EmitParamJson(array);
                sb.EmitPushString("balanceOf");
                sb.EmitAppCall(new Hash160("0xbab964febd82c9629cc583596975f51811f25f47"));
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
    }
}
