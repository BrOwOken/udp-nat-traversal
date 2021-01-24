using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using STUN;
using STUN.Attributes;

namespace UdpNatStun
{
    class Program
    {
        static void Main(string[] args)
        {
            // STUNUtils.TryParseHostAndPort("stun.l.google.com", out IPEndPoint stunEndPoint);
            var address = Dns.GetHostAddresses("stun.l.google.com");
            var stunep = new IPEndPoint(address[0], 19302);


            var stunResult = STUNClient.Query(stunep, STUNQueryType.ExactNAT, true);

            Console.WriteLine($"loaclEp: {stunResult.LocalEndPoint}");
            Console.WriteLine($"publicEp: {stunResult.PublicEndPoint}");

            var udpClient = new UdpClient(stunResult.LocalEndPoint);

            var ipBytes = stunResult.PublicEndPoint.Address.GetAddressBytes();
            var portBytes = BitConverter.GetBytes(((UInt16)(stunResult.PublicEndPoint.Port)));
            var magicToken = new byte[ipBytes.Length + portBytes.Length];

            ipBytes.CopyTo(magicToken, 0);
            portBytes.CopyTo(magicToken, ipBytes.Length);

            Console.WriteLine($"Muj magicToken: {Convert.ToBase64String(magicToken)}");


            var th = new Thread(Receive);
            th.Start(udpClient);

            var remoteEps = new List<IPEndPoint>();

            while (true)
            {
                var cmd = Console.ReadLine();
                if (cmd.StartsWith("/a"))
                {
                    Console.WriteLine("Remote magicToken:");
                    magicToken = Convert.FromBase64String(Console.ReadLine());

                    var remoteEp = new IPEndPoint(new IPAddress(
                        magicToken[0..^2]),
                        BitConverter.ToUInt16(magicToken[^2..]));
                    remoteEps.Add(remoteEp);

                    var ms = Encoding.ASCII.GetBytes("hewwo");
                    udpClient.Send(ms, ms.Length, remoteEp);
                }
                else if (cmd.StartsWith("/l"))
                {
                    foreach (var ep in remoteEps)
                        Console.WriteLine(ep.ToString());
                }
                else
                {
                    var ms = Encoding.ASCII.GetBytes(cmd);
                    foreach (var ep in remoteEps)
                        udpClient.Send(ms, ms.Length, ep);
                }
            }
        }


        static void Receive(object args)
        {
            var client = args as UdpClient;

            while (true)
            {
                try
                {
                    var remoteEp = new IPEndPoint(IPAddress.Any, 0);
                    client.AllowNatTraversal(true);
                    var ms = client.Receive(ref remoteEp);
                    Console.WriteLine($"{remoteEp} -> {Encoding.ASCII.GetString(ms)}");
                }
                catch(Exception e)
                {
                    Console.WriteLine(e.Message);
                }
                
            }
        }
    }
}