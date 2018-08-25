using System;
using System.IO;
using System.Net;
using System.Text;

namespace GetBlockData
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Start!");

            try
            {
                DumpBlock();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

        }

        private static WebClient wc = new WebClient();

        private static void DumpBlock()
        {
            var folder = "E:\\TestNetData\\";
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var height = GetHeight();
            var localheight = Directory.GetFiles("E:\\TestNetData\\").Length;
            for (int i = localheight; i < height; i++)
            {
                var blockdata = GetBlock(i);
                var path = folder + Path.DirectorySeparatorChar + i.ToString("D08") + ".txt";
                File.Delete(path);
                File.WriteAllText(path, blockdata, Encoding.UTF8);
                if (i % 1000 == 0)
                {
                    Console.WriteLine("height = " + i);
                }
            }

        }

        private static int GetHeight()
        {
            var url = "http://127.0.0.1:20332/?jsonrpc=2.0&id=1&method=getblockcount&params=[]";
            var info = wc.DownloadString(url);
            var json = Newtonsoft.Json.Linq.JObject.Parse(info);
            int count = (int)json["result"];
            return count;
        }

        private static string GetBlock(int height)
        {
            var url = "http://127.0.0.1:20332/?jsonrpc=2.0&id=1&method=getblock&params=[" + height + ",1]";
            var info = wc.DownloadString(url);
            var json = Newtonsoft.Json.Linq.JObject.Parse(info);
            string block = json["result"].ToString();
            return block;
        }
    }
}
