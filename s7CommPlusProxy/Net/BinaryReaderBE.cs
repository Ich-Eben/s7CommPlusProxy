using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace s7CommPlusProxy {
    class BinaryReaderBE : BinaryReader {
        public BinaryReaderBE(Stream input) : base(input) {
        }

        public BinaryReaderBE(Stream input, Encoding encoding) : base(input, encoding) {
        }

        public BinaryReaderBE(Stream input, Encoding encoding, bool leaveOpen) : base(input, encoding, leaveOpen) {
        }

        public int ReadUInt16BE() {
            byte[] buffer = base.ReadBytes(2);
            Array.Reverse(buffer);
            return BitConverter.ToUInt16(buffer, 0);
        }
    }
}
