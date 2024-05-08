using OpenSsl;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace s7CommPlusProxy {
    class ServerConnection : OpenSSLConnector.IConnectorCallback {

        public delegate void isoConnectionRequest(IsoConnectionParam param);
        public isoConnectionRequest onIsoConnectionRequest;
        public delegate void dataReceived(byte[] data);
        public dataReceived onDataReceived;
        public delegate void clientDisconnect();
        public clientDisconnect onClientDisconnect;

        private bool tlsActive = false;

        Socket socket;
        Thread thread;
        bool stopThread;

        IntPtr m_ptr_ssl_method;
        IntPtr m_ptr_ctx;
        OpenSSLConnector m_sslconn;
        Native.SSL_CTX_keylog_cb_func m_keylog_cb;

        public DateTime startTime;

        public void start(Socket socket) {
            this.socket = socket;
            this.socket.NoDelay = true;

            stopThread = false;
            thread = new Thread(recThreadCall);
            thread.Start();
        }

        public void close() {
            if (socket.Connected) socket.Close();
            stopThread = true;
            thread.Join();
        }

        private void recThreadCall() {
            const int bufferSize = 4096;
            byte[] buffer = new byte[bufferSize];
            int writePointer = 0;
            int readPointer = 0;

            try {
                while (!stopThread && socket.Connected) {
                    int count = socket.Receive(buffer, writePointer, bufferSize - writePointer, 0);
                    writePointer += count;
                    if (count > 0) {
                        readPointer = 0;
                        int parsedLen;
                        do {
                            parsedLen = parseIsoPackage(new MemoryStream(buffer, readPointer, writePointer - readPointer));
                            readPointer += parsedLen;
                        } while (parsedLen > 0);
                        //move buffer
                        if (readPointer > 0) {
                            int bytesLeft = writePointer - readPointer;
                            if (bytesLeft > 0) {
                                Console.WriteLine("buffer not empty!");
                            }
                            for (int i = 0; i < bytesLeft; ++i) buffer[i] = buffer[i + readPointer];
                            writePointer = bytesLeft;
                        }
                    }
                }
            } catch (System.Net.Sockets.SocketException ex) {
                Console.WriteLine(ex.ToString());
            }
            if (socket.Connected) {
                socket.Close();
            }
            onClientDisconnect?.Invoke();
            tlsActive = false;
        }

        private int parseIsoPackage(MemoryStream ms) {
            //COTP
            if (ms.Length < 4) return 0;
            BinaryReaderBE br = new BinaryReaderBE(ms);
            byte version = br.ReadByte();
            if (version != 0x03) return 1;
            br.ReadByte(); //reserved
            int packetLen = br.ReadUInt16BE();
            if (br.BaseStream.Length - br.BaseStream.Position < packetLen - 4) return 0;
            //ISO
            int headerLen = br.ReadByte();
            if (br.BaseStream.Length - br.BaseStream.Position < headerLen) return 0;
            int pduType = br.ReadByte();

            switch (pduType) {
                case IsoConsts.PDU_TYPE_CONNECT_REQUEST:
                    IsoConnectionParam conParam = new IsoConnectionParam();
                    conParam.dstRef = br.ReadUInt16BE();
                    conParam.srcRef = br.ReadUInt16BE();
                    conParam.cl = br.ReadByte();
                    conParam.pduSize = 0;

                    for (int i = 0; i < 3; ++i) {
                        int paramCode = br.ReadByte();
                        int paramLen = br.ReadByte();
                        if (br.BaseStream.Length - br.BaseStream.Position < paramLen) return 0;
                        switch (paramCode) {
                            case IsoConsts.PDU_PARAM_SIZE:
                                conParam.pduSize = br.ReadByte();
                                break;
                            case IsoConsts.PDU_PARAM_SRC_TSAP:
                                conParam.srcTsap = new byte[paramLen];
                                conParam.srcTsap = br.ReadBytes(paramLen);
                                break;
                            case IsoConsts.PDU_PARAM_DST_TSAP:
                                conParam.dstTsap = new byte[paramLen];
                                conParam.dstTsap = br.ReadBytes(paramLen);
                                break;
                            default:
                                throw new Exception("Unknowen PDU param type");
                        }
                    }
                    onIsoConnectionRequest?.Invoke(conParam);

                    return packetLen;
                case IsoConsts.PDU_TYPE_DATA:
                    byte pduNo = br.ReadByte();
                    bool last = (pduNo & 0x80) == 0x80;
                    pduNo &= 0x7F;

                    int payloadLen = packetLen - headerLen - 1 - 4;
                    if (!last && payloadLen > 0) throw new Exception("Last bit not set!"); //TODO: Handle "last" bit not set and stitch multiple pdus together?
                    byte[] payload = new byte[payloadLen];
                    payload = br.ReadBytes(payloadLen);

                    if (!tlsActive) {
                        onDataReceived?.Invoke(payload);
                    } else {
                        //tls decrypt
                        m_sslconn.ReadCompleted(payload, payload.Length);
                    }

                    return packetLen;
                default:
                    throw new Exception("Unknowen PDU type");
            }
        }

        public void sendIsoPackage(byte[] payload, int payloadLen) {
            byte[] buffer = new byte[4096];
            MemoryStream ms = new MemoryStream(buffer);
            BinaryWriterBE bw = new BinaryWriterBE(ms);
            bw.Write((Byte)0x03); //version
            bw.Write((Byte)0x00); //reserved
            int packetLen = 4 + 3 + payloadLen;
            bw.WriteUInt16BE(packetLen); //packetLen

            bw.Write((byte)2); //headerLen
            bw.Write((byte)IsoConsts.PDU_TYPE_DATA); //pud type
            bw.Write((byte)0x80); //pdu id + last
            bw.Write(payload, 0, payloadLen);

            if (socket.Connected) socket.Send(buffer, packetLen, 0);
        }

        public void sendConnectionResponse(IsoConnectionParam param) {
            byte[] buffer = new byte[4096];
            MemoryStream ms = new MemoryStream(buffer);
            BinaryWriterBE bw = new BinaryWriterBE(ms);
            bw.Write((Byte)0x03); //version
            bw.Write((Byte)0x00); //reserved
            int packetLen = 4 + 14 + param.srcTsap.Length + param.dstTsap.Length;
            bw.WriteUInt16BE(packetLen); //packetLen

            bw.Write((byte)(packetLen - 4 - 1)); //headerLen
            bw.Write((byte)IsoConsts.PDU_TYPE_CONNECT_RESPONSE); //pud type
            bw.WriteUInt16BE(param.dstRef); //dstRef
            bw.WriteUInt16BE(param.srcRef); //srcRef
            bw.Write((byte)param.cl); //class++
            bw.Write((byte)IsoConsts.PDU_PARAM_SIZE); //param code
            bw.Write((byte)1); //paramLen
            bw.Write((byte)param.pduSize); //PDU size
            bw.Write((byte)IsoConsts.PDU_PARAM_SRC_TSAP); //param code
            bw.Write((byte)param.srcTsap.Length); //paramLen
            bw.Write(param.srcTsap); //srcTsap
            bw.Write((byte)IsoConsts.PDU_PARAM_DST_TSAP); //param code
            bw.Write((byte)param.dstTsap.Length); //paramLen
            bw.Write(param.dstTsap); //dstTsap


            socket.Send(buffer, packetLen, 0);
        }

        public void send(byte[] data, bool forceUnencrypted = false) {
            if (!tlsActive || forceUnencrypted) {
                sendIsoPackage(data, data.Length);
            } else {
                //tls encrypt
                m_sslconn.Write(data, data.Length);
            }
        }

        // OpenSSL Key Callback Funktion. Gibt die ausgehandelden privaten Schlüssel aus. Kann beispielsweise
        // in eine Wireshark Aufzeichnung eingefügt werden um dort die TLS Kommunikation zu entschlüsseln.
        public void SSL_CTX_keylog_cb(IntPtr ssl, string line) {
            string filename = "key_" + startTime.ToString("yyyyMMdd_HHmmss") + ".log";
            StreamWriter file = new StreamWriter(filename, append: true);
            file.WriteLine(line);
            file.Close();
        }

        // Startet OpenSSL und aktiviert ab jetzt TLS
        public void SslActivate() {
            int ret;
            try {
                ret = Native.OPENSSL_init_ssl(0, IntPtr.Zero); // returns 1 on success or 0 on error
                if (ret != 1) {
                    throw new Exception("SSL Error");
                }
                m_ptr_ssl_method = Native.ExpectNonNull(Native.TLS_server_method());
                m_ptr_ctx = Native.ExpectNonNull(Native.SSL_CTX_new(m_ptr_ssl_method));
                // TLS 1.3 forcieren, da wegen TLS on IsoOnTCP bekannt sein muss, um wie viele Bytes sich die verschlüsselten
                // Daten verlängern um die Pakete auf S7CommPlus-Ebene entsprechend zu fragmentieren.
                // Die Verlängerung geschieht z.B. durch Padding und HMAC. Bei TLS 1.3 existiert mit GCM kein Padding und verlängert sich immer
                // um 16 Bytes. Da auch TLS_CHACHA20_POLY1305_SHA256 zu den TLS 1.3  CipherSuite zählt, explizit die anderen setzen.
                Native.SSL_CTX_ctrl(m_ptr_ctx, Native.SSL_CTRL_SET_MIN_PROTO_VERSION, Native.TLS1_3_VERSION, IntPtr.Zero);
                ret = Native.SSL_CTX_set_ciphersuites(m_ptr_ctx, "TLS_AES_256_GCM_SHA384:TLS_AES_128_GCM_SHA256");
                if (ret != 1) {
                    throw new Exception("SSL Error");
                }

                Native.SSL_CTX_use_certificate_file(m_ptr_ctx, "test.crt.pem", Native.SSL_FILETYPE_PEM);
                Native.SSL_CTX_use_PrivateKey_file(m_ptr_ctx, "test.key.pem", Native.SSL_FILETYPE_PEM);

                m_sslconn = new OpenSSLConnector(m_ptr_ctx, this);
                m_sslconn.ExpectConnect(true);

                // Keylog callback setzen
                m_keylog_cb = new Native.SSL_CTX_keylog_cb_func(SSL_CTX_keylog_cb);
                Native.SSL_CTX_set_keylog_callback(m_ptr_ctx, m_keylog_cb);

                tlsActive = true;
            } catch (Exception ex) {
                throw new Exception("SSL Error");
            }
        }

        //out (decrypted)
        public void OnDataAvailable() {
            // Netzwerk meldet eintreffende Daten
            byte[] buf = new byte[8192];
            int bytesRead = m_sslconn.Receive(ref buf, buf.Length);
            byte[] readData = new byte[bytesRead];
            Array.Copy(buf, readData, bytesRead);
            onDataReceived?.Invoke(readData);
        }

        //openSSL --> socket (encrypted)
        public void WriteData(byte[] pData, int dataLength) {
            sendIsoPackage(pData, dataLength);
        }



    }
}
