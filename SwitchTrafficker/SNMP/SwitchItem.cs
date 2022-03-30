using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SwitchTrafficker
{
    public class SwitchItem
    {
        public string name { get; set; } = string.Empty;
        public string ip { get; set; } = string.Empty;
        public int port { get; set; } = 161;
        public string community { get; set; } = string.Empty;
        public int interval { get; set; } = 0;
        
        public IPEndPoint endpoint
        {
            get => new IPEndPoint(IPAddress.Parse(ip), port);
        }
    }
}
