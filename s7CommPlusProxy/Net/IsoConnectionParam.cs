using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace s7CommPlusProxy {
    public struct IsoConnectionParam {
        public int dstRef;
        public int srcRef;
        public byte cl;
        public byte pduSize;
        public byte[] srcTsap;
        public byte[] dstTsap;
    }

}
