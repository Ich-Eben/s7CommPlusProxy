using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace s7CommPlusProxy {

    class IsoConsts {

        public const int PDU_TYPE_CONNECT_REQUEST = 0xE0;
        public const int PDU_TYPE_CONNECT_RESPONSE = 0xD0;
        public const int PDU_TYPE_DATA = 0xF0;

        public const int PDU_PARAM_SIZE = 0xC0;
        public const int PDU_PARAM_SRC_TSAP = 0xC1;
        public const int PDU_PARAM_DST_TSAP = 0xC2;

    }

}
