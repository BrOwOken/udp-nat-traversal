using System;
using System.Collections.Generic;
using System.Text;

namespace UdpNatStun
{
    class Packet
    {
        public int Cmd { get; set; }
        public string Message { get; set; }

        public override string ToString()
        {
            return $"[{Cmd}][{Message}]";
        }
        public Packet(int cmd, string msg)
        {
            Cmd = cmd;
            Message = msg;
        }
    }
}
