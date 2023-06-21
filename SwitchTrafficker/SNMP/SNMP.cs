using InfluxDB.Client.Writes;
using Lextm.SharpSnmpLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SwitchTrafficker
{
    public class SNMP
    {
        public static List<PointData> ProcessPorts(List<Variable> ports, List<Variable> bytesIn, List<Variable> bytesOut, List<Variable> desc, List<Variable> macs, SwitchItem sw, DateTime timeStamp)
        {
            List<PointData> points = new List<PointData>();

            foreach (var port in ports.OrderBy(o => o.Id))
            {
                var portID = port.Id.ToString().Split('.').Last();

                long portBytesIn = long.Parse(bytesIn.Where(x => x.Id.ToString().EndsWith(portID)).FirstOrDefault()?.Data.ToString() ?? "0");
                long portBytesOut = long.Parse(bytesOut.Where(x => x.Id.ToString().EndsWith(portID)).FirstOrDefault()?.Data.ToString() ?? "0");
                var portMacBytes = macs.Where(x => x.Id.ToString().EndsWith(portID)).FirstOrDefault()?.Data.ToBytes();
                string portMac = DecodeOctetString(portMacBytes);
                string portdesc = desc.Where(x => x.Id.ToString().EndsWith(portID)).FirstOrDefault()?.Data.ToString() ?? string.Empty;
                string portName = port?.Data.ToString() ?? "";

                var point = PointData.Measurement("bytesIn")
                    .Tag("mac", portMac)
                    .Tag("name", portName)
                    .Field(sw.name, portBytesIn)
                    .Timestamp(timeStamp, InfluxDB.Client.Api.Domain.WritePrecision.Ms);

                points.Add(point);

                point = PointData.Measurement("bytesOut")
                    .Tag("mac", portMac)
                    .Tag("name", portName)
                    .Field(sw.name, portBytesOut)
                    .Timestamp(timeStamp, InfluxDB.Client.Api.Domain.WritePrecision.Ms);

                points.Add(point);

                point = PointData.Measurement("portDesc")
                    .Tag("mac", portMac)
                    .Tag("name", portName)
                    .Field(sw.name, portdesc)
                    .Timestamp(timeStamp, InfluxDB.Client.Api.Domain.WritePrecision.Ms);

                points.Add(point);
            }
            return points;
        }

        private static string DecodeOctetString(byte[] raw)
        {
            //First 2 bytes are the Type, so remove them
            byte[] bytes = new byte[raw.Length - 2];
            Array.Copy(raw, 2, bytes, 0, bytes.Length);

            //Check if there are any non-ascii characters
            bool ascii = true;
            foreach (char c in Encoding.UTF8.GetString(bytes))
            {
                if (c >= 128)
                {
                    ascii = false;
                }
            }

            //If it's all ascii, return as ascii, else convert to hex
            return ascii ? Encoding.ASCII.GetString(bytes) : BitConverter.ToString(bytes);
        }
    }
}
