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
        public static List<PointData> ProcessPorts(List<Variable> ports, List<Variable> bytesIn, List<Variable> bytesOut, SwitchItem sw, DateTime timeStamp)
        {
            List<PointData> points = new List<PointData>(); 
            foreach (var port in ports)
            {
                var portID = port.Id.ToString().Split('.').Last();

                long portBytesIn = long.Parse(bytesIn.Where(x => x.Id.ToString().EndsWith(portID)).FirstOrDefault()?.Data.ToString() ?? "0");
                long portBytesOut = long.Parse(bytesOut.Where(x => x.Id.ToString().EndsWith(portID)).FirstOrDefault()?.Data.ToString() ?? "0");
                string portName = port?.Data.ToString() ?? "";

                var point = PointData.Measurement("bytesIn")
                    .Tag("name", portName)
                    .Field(sw.name, portBytesIn)
                    .Timestamp(timeStamp, InfluxDB.Client.Api.Domain.WritePrecision.Ms);

                points.Add(point);

                point = PointData.Measurement("bytesOut")
                    .Tag("name", portName)
                    .Field(sw.name, portBytesOut)
                    .Timestamp(timeStamp, InfluxDB.Client.Api.Domain.WritePrecision.Ms);

                points.Add(point);
            }
            return points;
        }
    }
}
