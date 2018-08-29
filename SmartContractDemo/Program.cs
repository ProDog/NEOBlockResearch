using System;
using System.Collections.Generic;
using System.Linq;
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

            PubScDemoAsync(wif);

            
        }

        private static async void PubScDemoAsync(string wif)
        {
            string assetid = "0x602c79718b16e442de58778e148d0b1084e3b2dffd5de6b7b16cee7969282de7";
            string api = "https://api.nel.group/api/testnet";
            byte[] prikey = ThinNeo.Helper.GetPrivateKeyFromWIF(wif);
            byte[] pubkey = ThinNeo.Helper.GetPublicKeyFromPrivateKey(prikey);
            string address = ThinNeo.Helper.GetAddressFromPublicKey(pubkey);

            Dictionary<string, List<UTXO>> dic_UTXO = new Dictionary<string, List<UTXO>>();

            //读取合约avm文件
            byte[] script = System.IO.File.ReadAllBytes("NeoContracts.avm");
            Console.WriteLine("合约脚本：" + ThinNeo.Helper.Bytes2HexString(script));
            Console.WriteLine("合约脚本哈希:" + ThinNeo.Helper.Bytes2HexString(ThinNeo.Helper.GetScriptHashFromScript(script)
                                  .data.ToArray().Reverse().ToArray()));
            byte[] parameter_list = ThinNeo.Helper.HexString2Bytes("0712");//合约入参
            byte[] return_type = ThinNeo.Helper.HexString2Bytes("02");//合约出参
            int need_storage = 1;
            int need_nep4 = 0;
            int need_canCharge = 4;
            string name = "xy";
            string version = "1.0";
            string auther = "zhang";
            string email = "gripzhang";
            string description = "test";
            using (ThinNeo.ScriptBuilder sb = new ScriptBuilder())
            {
                var ss = need_storage | need_nep4 | need_canCharge;

                sb.EmitPushString(description);
                sb.EmitPushString(email);
                sb.EmitPushString(auther);
                sb.EmitPushString(version);
                sb.EmitPushString(name);
                sb.EmitPushNumber(need_storage | need_nep4 | need_canCharge);
                sb.EmitPushBytes(return_type);
                sb.EmitPushBytes(parameter_list);
                sb.EmitPushBytes(script);
                sb.EmitSysCall("Neo.Contract.Create");

                string scriptPublish = ThinNeo.Helper.Bytes2HexString(sb.ToArray());
                
                byte[] postdata;
                var method = "invokescript";
                var url = Helper.MakeRpcUrlPost(api, method, out postdata,
                    new MyJson.JsonNode_ValueString(scriptPublish));
                var result = await Helper.HttpPost(url, postdata);

                var consume =
                    (((MyJson.Parse(result) as MyJson.JsonNode_Object)["result"] as MyJson.JsonNode_Array)[0] as
                        MyJson.JsonNode_Object)["gas_consumed"].ToString();
                decimal gas_consume = decimal.Parse(consume);
                ThinNeo.InvokeTransData extdata = new InvokeTransData();
                extdata.script = sb.ToArray();
                extdata.gas = Math.Ceiling(gas_consume - 10);

                ThinNeo.Transaction tran = MakeTran(dic_UTXO, null, new ThinNeo.Hash256(assetid), extdata.gas);

                tran.version = 1;
                tran.extdata = extdata;
                tran.type = ThinNeo.TransactionType.InvocationTransaction;
                byte[] msg = tran.GetMessage();
                byte[] signdata = ThinNeo.Helper.Sign(msg, prikey);
                tran.AddWitness(signdata, pubkey, address);
                string txid = tran.GetHash().ToString();
                byte[] data = tran.GetRawData();
                string rawdata = ThinNeo.Helper.Bytes2HexString(data);

                url = Helper.MakeRpcUrlPost(api, "sendrawtransaction", out postdata,
                    new MyJson.JsonNode_ValueString(rawdata));
                result = await Helper.HttpPost(url, postdata);
                MyJson.JsonNode_Object resJO = (MyJson.JsonNode_Object) MyJson.Parse(result);
                Console.WriteLine(resJO.ToString());
            }
        }

        private static Transaction MakeTran(Dictionary<string, List<UTXO>> dic_UTXO, string targetAddr, Hash256 assetid, decimal sendCount)
        {
            if(!dic_UTXO.ContainsKey(assetid.ToString()))
                throw new Exception("No Money!");
            List<UTXO> utxos = dic_UTXO[assetid.ToString()];
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
    }
}
