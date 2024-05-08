using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace s7CommPlusProxy {
    class BinaryWriterBE: BinaryWriter {

        public BinaryWriterBE(Stream output) : base(output) {
        }

        public BinaryWriterBE(Stream output, Encoding encoding) : base(output, encoding) {
        }

        public BinaryWriterBE(Stream output, Encoding encoding, bool leaveOpen) : base(output, encoding, leaveOpen) {
        }

        public void WriteUInt16BE(int val) {
            UInt16 i = (UInt16)val;
            byte[] buffer = new byte[2];
            buffer[1] = (byte)i;
            buffer[0] = (byte)(i >> 8);
            Write(buffer);
        }
    }
}
