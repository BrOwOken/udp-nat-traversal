using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using STUN;
using STUN.Attributes;

namespace UdpNatStun
{
    class Program
    {
        public static Regex Re = new Regex(@"\[(?<cmd>[0-9])\]\[(?<value>.*)\]");
        public static List<IPEndPoint> remoteEps = new List<IPEndPoint>();
        private static Thread keepAlive = new Thread(KeepAliveSend);
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
            keepAlive.Start(udpClient);

            

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
                    var ms2 = Encoding.ASCII.GetBytes($"[2][{magicToken}]");
                    foreach (var ep in remoteEps)
                    {
                        udpClient.Send(ms2, ms2.Length, ep);
                    }
                }
                else if (cmd.StartsWith("/l"))
                {
                    foreach (var ep in remoteEps)
                        Console.WriteLine(ep.ToString());
                }
                else
                {
                    var ms = Encoding.ASCII.GetBytes($"[0][{cmd}]");
                    foreach (var ep in remoteEps)
                        try
                        {
                            udpClient.Send(ms, ms.Length, ep);
                        }
                        catch (Exception e)
                        {

                            Console.WriteLine(e.Message);
                        }
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
                    var ss = Encoding.ASCII.GetString(ms);

                    var result = Re.Match(ss);
                    if (result.Success)
                    {
                        var packet = new Packet(int.Parse(result.Groups["cmd"].Value), result.Groups["value"].Value);
                        switch (packet.Cmd)
                        {
                            case 0:
                                Console.WriteLine(packet.Message);
                                break;
                            case 1:
                                var msg = Encoding.ASCII.GetBytes("[2][]");
                                client.Send(msg,msg.Length, remoteEp);
                                break;
                            case 3:
                                var magicPackets = packet.Message.Split(';');
                                var adds = new List<string>();
                                foreach(var pckt in magicPackets)
                                {
                                    var ep = ToEndpoint(pckt);
                                    if (remoteEps.Contains(ep))
                                    {
                                        continue;
                                    }
                                    remoteEps.Add(ep);
                                    adds.Add(pckt);
                                }

                                var res = Encoding.ASCII.GetBytes(string.Join(';', adds));
                                foreach (var ep in remoteEps)
                                {
                                    client.Send(res, res.Length, ep);
                                }
                                break;
                        }
                        
                    }
                    Console.WriteLine($"{remoteEp} -> {Encoding.ASCII.GetString(ms)}");
                }
                catch(Exception e)
                {
                    Console.WriteLine(e.Message);
                }
                
            }
        }
        public static IPEndPoint ToEndpoint(string token)
        {
            try
            {
                var magicToken = Convert.FromBase64String(token);
                var remoteEp = new IPEndPoint(
                    new IPAddress(magicToken[0..^2]),
                    BitConverter.ToUInt16(magicToken[^2..]));

                return remoteEp;
            }
            catch (Exception e)
            {
                return null;
            }
        }
        public static void KeepAliveSend(object args)
        {
            var client = args as UdpClient;
            while (true)
            {
                Thread.Sleep(30000);
                var bytes = Encoding.ASCII.GetBytes("[1][]");
                foreach (var ep in remoteEps)
                {
                    try
                    {
                        client.Send(bytes, bytes.Length, ep);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                   
                }
            }
        }
    }
}