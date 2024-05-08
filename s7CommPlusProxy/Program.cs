using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection.Metadata.Ecma335;
using System.ServiceProcess;
using s7CommPlusProxy;


IPAddress plcIp = IPAddress.Parse("192.168.1.30");
IPAddress bindIp = IPAddress.Parse("0.0.0.0");
if (args.Length >= 1) {
    plcIp = IPAddress.Parse(args[0]);
}
if (args.Length >= 2) {
    bindIp = IPAddress.Parse(args[1]);
}

ServiceController sc = new ServiceController("S7DOS Help Service");

//stop s7Dos service before we start listening on port 102 as it binds to all interfaces by default
bool s7dosServiceWasRunning = false;
try {
    if (sc.Status == ServiceControllerStatus.Running) {
        Console.WriteLine("stopping S7 service...");
        sc.Stop();
        sc.WaitForStatus(ServiceControllerStatus.Stopped);
        s7dosServiceWasRunning = true;
        Console.WriteLine("S7 service stopped");
    }
} catch (Exception ex) { }


DateTime dt = DateTime.Now;
IsoServer isoServer = new IsoServer(dt);

void clientConnected(ServerConnection conn) {
    IsoClient isoClient = new IsoClient(dt);

    //server
    conn.onIsoConnectionRequest = (IsoConnectionParam param) => {
        isoClient.connect(plcIp);
        isoClient.sendConnectionRequest(param);
    };
    conn.onDataReceived = (byte[] data) => {
        isoClient.send(data);
    };
    conn.onClientDisconnect = () => {
        isoClient.close();
    };

    //client
    isoClient.onIsoConnectionResponse = (IsoConnectionParam param) => {
        conn.sendConnectionResponse(param);
    };
    isoClient.onClientDataReceived = (byte[] data) => {
        if (data.Length >= 24 && data[0] == 0x72 && data[4] == 0x32 && data[7] == 0x05 && data[8] == 0xb3) {
            //initSSL response detected
            conn.SslActivate();
            isoClient.SslActivate();
            conn.send(data, true);
            Console.WriteLine("TLS active!");
        } else {
            conn.send(data);
        }
    };
}
isoServer.onClientConnected += clientConnected;

isoServer.listen(bindIp);
Console.WriteLine("Waiting for incomming connections...");


if (s7dosServiceWasRunning) {
    Console.WriteLine("starting S7 service...");
    sc.Start();
    sc.WaitForStatus(ServiceControllerStatus.Running);
    Console.WriteLine("S7 service started");
}


