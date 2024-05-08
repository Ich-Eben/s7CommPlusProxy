using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net.Sockets;
using OpenSsl;
using System.Net;
using System.Numerics;
using static s7CommPlusProxy.IsoServer;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace s7CommPlusProxy {

    class IsoServer {

        public delegate void clientConnected(ServerConnection conn);
        public clientConnected onClientConnected;

        private Socket server;
        private Thread serverThread;
        private bool stopServerThread = false;
        private List<ServerConnection> connections;

        private DateTime startTime;

        public IsoServer(DateTime startTime) {
            this.startTime = startTime;
            connections = new List<ServerConnection>();
        }
        ~IsoServer() {
            close();
        }

        public void listen(IPAddress ipAddress) {
            server = new(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            server.NoDelay = true;

            IPEndPoint endpoint = new IPEndPoint(ipAddress, 102);
            server.Bind(endpoint);
            server.Listen();

            //start receive thread
            stopServerThread = false;
            serverThread = new Thread(serverThreadCall);
            serverThread.Start();
        }

        public void close() {
            //stopThreads = true;
            stopServerThread = true;
            serverThread?.Join();

            //go through all clients and disconnect
            foreach (var conn in connections) {
                conn.close();
            }
            server?.Close();
        }

        private void serverThreadCall() {
            while (!stopServerThread) {
                Socket socket = server.Accept();
                ServerConnection conn = new ServerConnection();
                onClientConnected?.Invoke(conn);
                conn.startTime = startTime;
                conn.start(socket);
            }
        }

        
    }
}
