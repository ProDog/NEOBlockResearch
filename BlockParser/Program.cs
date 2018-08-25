using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BlockParser
{
    class Program
    {
        static string neo = "0xc56f33fc6ecfcd0c225c4ab356fee59390af8560be0e930faebe74a6daff7c9b";
        static string gas = "0x602c79718b16e442de58778e148d0b1084e3b2dffd5de6b7b16cee7969282de7";

        private static Dictionary<string, TX> allTrans = new Dictionary<string, TX>();
        private static Dictionary<string, UserAddr> allAddrs = new Dictionary<string, UserAddr>();

        private static string path = "E:\\TestNetData\\";

        static void Main(string[] args)
        {
            Console.WriteLine("Start!");
            string text = path + Path.DirectorySeparatorChar.ToString() + "ParserHeight.txt";
            int num = 0;
            if (File.Exists(path + Path.DirectorySeparatorChar.ToString() + "AllTrans.txt"))
            {
                allTrans = JsonConvert.DeserializeObject<Dictionary<string, TX>>(File.ReadAllText(path + Path.DirectorySeparatorChar.ToString() + "AllTrans.txt"));
            }
            if (File.Exists(path + Path.DirectorySeparatorChar.ToString() + "AllAddrs.txt"))
            {
                allAddrs = JsonConvert.DeserializeObject<Dictionary<string, UserAddr>>(File.ReadAllText(path + Path.DirectorySeparatorChar.ToString() + "AllAddrs.txt"));
            }
            if (File.Exists(text))
            {
                num = int.Parse(File.ReadAllText(path + Path.DirectorySeparatorChar.ToString() + "ParserHeight.txt"));
            }
            while (true)
            {
                string text2 = path + num.ToString("D08") + ".txt";
                if (!File.Exists(text2))
                {
                    break;
                }
                if (num % 1000 == 0)
                {
                    Console.WriteLine("height = " + num);
                }
                string text3 = File.ReadAllText(text2, Encoding.UTF8);
                JObject json = JObject.Parse(text3);
                ParserJson(json);
                num++;
            }
            File.WriteAllText(path + Path.DirectorySeparatorChar.ToString() + "ParserHeight.txt", num.ToString(), Encoding.UTF8);
            Console.WriteLine("Block Height = " + num);
            string contents = JsonConvert.SerializeObject((object)allTrans);
            File.WriteAllText(path + "AllTrans.txt", contents, Encoding.UTF8);
            string contents2 = JsonConvert.SerializeObject((object)allAddrs);
            File.WriteAllText(path + "AllAddrs.txt", contents2, Encoding.UTF8);
            Console.WriteLine("Sync Over.");
            GetAddressAeest();
            Console.ReadKey();
        }

        private static void GetAddressAeest()
        {
            while (true)
            {
                Console.WriteLine("Please Input Your Address:");
                string text = Console.ReadLine();
                Console.WriteLine("address = " + text);
                Dictionary<string, decimal> coins = new Dictionary<string, decimal>();
                if (!allAddrs.ContainsKey(text))
                {
                    Console.WriteLine("Not exists this address!");
                    continue;
                }
                
                    foreach (string utxo in allAddrs[text].utxos)
                    {
                        string[] array = utxo.Split('_');
                        string key = array[0];
                        int num = int.Parse(array[1]);
                        VOUT coin = allTrans[key].vout[num];
                        if (!coins.ContainsKey(coin.asset))
                        {
                            coins[coin.asset] = decimal.Zero;
                        }

                        coins[coin.asset] += coin.value;
                    }
                    if (coins.Count == 0)
                    {
                        Console.WriteLine("No Coin");
                    }
                    foreach (KeyValuePair<string, decimal> item in coins)
                    {
                        string text2 = item.Key;
                        if (text2 == neo)
                        {
                            text2 = "<NEO>";
                        }
                        if (text2 == gas)
                        {
                            text2 = "<GAS>";
                        }
                        Console.WriteLine(text2 + " = " + item.Value);
                    }
                
            }
        }

        static void ParserJson(JObject json)
        {
            var txs = (JArray) json["tx"];
            foreach (var tx in txs)
            {
                var objtx = new TX();
                objtx.txid = tx["txid"].ToString();
                var vin = (JArray) tx["vin"];
                var vout = (JArray) tx["vout"];

                objtx.vin = new VIN[vin.Count];
                objtx.vout = new VOUT[vout.Count];

                for (int i = 0; i < vin.Count; i++)
                {
                    objtx.vin[i] = new VIN();
                    objtx.vin[i].txid = vin[i]["txid"].ToString();
                    objtx.vin[i].vout = (int) vin[i]["vout"];
                }

                for (int i = 0; i < vout.Count; i++)
                {
                    objtx.vout[i] = new VOUT();
                    objtx.vout[i].n = (int) vout[i]["n"];
                    objtx.vout[i].value = (decimal)vout[i]["value"];
                    objtx.vout[i].address = (string) vout[i]["address"];
                    objtx.vout[i].asset = (string) vout[i]["asset"];
                }

                allTrans[objtx.txid] = objtx;
                ParseObjTx(objtx);
            }
            
        }

        static void ParseObjTx(TX tx)
        {
            foreach (var vin in tx.vin)
            {
                //vin的txid来自于其他tx的txid，每一笔vin输入来自于tx.txid=vin.txid的第vout个输出；
                var coin = allTrans[vin.txid].vout[vin.vout];
                var utxoid = vin.txid + "_" + vin.vout;
                allAddrs[coin.address].utxos.Remove(utxoid);//销毁原来的utxo
            }

            for (int i = 0; i < tx.vout.Length; i++)
            {
                var vout = tx.vout[i];
                if (!allAddrs.ContainsKey(vout.address))
                {
                    allAddrs[vout.address] = new UserAddr();
                }
                var utxoid = tx.txid + "_" + i;
                allAddrs[vout.address].utxos.Add(utxoid);//将vout的utxo增加到每个地址
            }
            
        }

        static void ShowResult()
        {
            Console.WriteLine("address count = " + allAddrs.Count);
            foreach (var addr in allAddrs)
            {
                Console.WriteLine("address = " + addr.Key);
                Dictionary<string, decimal> coins = new Dictionary<string, decimal>();

                foreach (var utxo in addr.Value.utxos)
                {
                    var words = utxo.Split('_');
                    var txid = words[0];
                    var vout = int.Parse(words[1]);
                    var coin = allTrans[txid].vout[vout];
                    if (!coins.ContainsKey(coin.asset))
                    {
                        coins[coin.asset] = 0;
                    }
                    coins[coin.asset] += coin.value;
                }

                if (coins.Count == 0)
                    Console.WriteLine("No Coin");

                foreach (var asset in coins)
                {
                    var assedid = asset.Key;
                    if (assedid == neo)
                        assedid = "<NEO>";
                    if (assedid == gas)
                        assedid = "<GAS>";
                    Console.WriteLine(assedid + " = " + asset.Value);
                }

                Console.WriteLine();
            }
        }
    }

    public class UserAddr
    {
        public List<string> utxos = new List<string>();
    }

    public class VIN
    {
        public string txid;
        public int vout;
    }

    public class VOUT
    {
        public int n;
        public string asset;
        public decimal value;
        public string address;
    }

    public class TX
    {
        public string txid;
        public VIN[] vin;
        public VOUT[] vout;
    }
}
