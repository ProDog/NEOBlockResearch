using System;
using System.Collections.Generic;
using System.Text;
using ThinNeo;

namespace SmartContractDemo
{
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
}
