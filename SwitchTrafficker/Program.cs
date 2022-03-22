using InfluxDB.Client;
using InfluxDB.Client.Writes;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using Lextm.SharpSnmpLib.Security;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SwitchTrafficker
{
    internal class Program
    {
        static async Task Main(string[] args)
        { 
            Logging.InitializeLogs();

            string[] config;

            string influxdb = string.Empty;
            string influxbucket = string.Empty;
            string influxtoken = string.Empty;
            string influxorg = string.Empty;
            string snmpversion = string.Empty;

            try
            {
                Console.WriteLine("Reading configuration file....");

                string configPath = Path.Combine(new string[] { ".", "SwitchTrafficker.conf" });
                config = File.ReadAllLines(configPath).Where(x => !x.StartsWith("#") && !string.IsNullOrWhiteSpace(x)).ToArray();

                if (config.Length <= 1)
                    throw new Exception("Invalid config file. Please edit SwitchTrafficker.conf", null);


                influxdb = config.Where(x => x.StartsWith("influxdb")).FirstOrDefault().Split('=', 2)[1].Trim();
                influxbucket = config.Where(x => x.StartsWith("influxbucket")).FirstOrDefault().Split('=', 2)[1].Trim();
                influxtoken = config.Where(x => x.StartsWith("influxtoken")).FirstOrDefault().Split('=', 2)[1].Trim();
                influxorg = config.Where(x => x.StartsWith("influxorg")).FirstOrDefault().Split('=', 2)[1].Trim();

                snmpversion = config.Where(x => x.StartsWith("snmpversion")).FirstOrDefault().Split('=', 2)[1].Trim();
            }
            catch (Exception ex)
            {
                Logging.WriteError("Problem reading log file", ex.Message);
                Console.WriteLine(ex.Message);
                return;
            }
                
            VersionCode snmpVersion = VersionCode.V2;

            switch (snmpversion.ToUpper())
            {
                case "V1":
                case "V2":
                case "V2c":
                case "V3":
                    snmpVersion = VersionCode.V2;
                    break;
                default:
                    break;
            }

            #region SNMPv3 not implemented
            //string snmpuser = string.Empty;
            //string snmppass = string.Empty;
            //string snmppriv = string.Empty;
            //
            //SHA256AuthenticationProvider auth = null;
            //AES256PrivacyProvider priv = null;
            //
            //if (config.Where(x => x.StartsWith("snmpuser")).Any())
            //    snmpuser = config.Where(x => x.StartsWith("snmpuser")).FirstOrDefault().Split('=', 2)[1];
            //
            //if (config.Where(x => x.StartsWith("snmppass")).Any())
            //    snmppass = config.Where(x => x.StartsWith("snmppass")).FirstOrDefault().Split('=', 2)[1];
            //
            //if (config.Where(x => x.StartsWith("snmppriv")).Any())
            //    snmppriv = config.Where(x => x.StartsWith("snmppriv")).FirstOrDefault().Split('=', 2)[1];
            //
            //if (!string.IsNullOrWhiteSpace(snmppass))
            //    auth = new SHA256AuthenticationProvider(new OctetString(snmppass));
            //
            //if (!string.IsNullOrWhiteSpace(snmppriv))
            //    priv = new AES256PrivacyProvider(new OctetString(snmppriv), auth);
            #endregion

            List<SwitchItem> switches = new List<SwitchItem>();

            Console.WriteLine("Parsing switch list....");
            foreach (var sw in config.Where(x => x.StartsWith("switch")))
            {
                string[] values = sw.Split('=')[1].Trim().Split(',');

                switches.Add(new SwitchItem
                {
                    name = values[0].Trim(),
                    ip = values[1].Trim(),
                    community = values[2].Trim(),
                });
            }

            var influxClient = InfluxDBClientFactory.Create(influxdb, influxtoken);

            Task[] tasks = new Task[switches.Count];

            int i = 0;
            foreach (var sw in switches)
            {
                Console.WriteLine($"Starting task for {sw.name}....");
                tasks[i] = Task.Factory.StartNew(() => SwitchLoop(sw, influxClient, influxbucket, influxorg, snmpVersion));
                i++;
            }

            TaskFactory taskFactory = new TaskFactory();

            await taskFactory.ContinueWhenAll(tasks, completedTasks => { });
        }

        static Task SwitchLoop(SwitchItem sw, InfluxDBClient influxClient, string influxBucket, string influxOrg, VersionCode snmpVersion)
        {
            while (true)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                List<Variable> ports = new List<Variable>();
                List<Variable> bytesIn = new List<Variable>();
                List<Variable> bytesOut = new List<Variable>();

                var timeStamp = DateTime.UtcNow;

                try
                {
                    Messenger.BulkWalk(snmpVersion, sw.endpoint, new OctetString(sw.community), null, new ObjectIdentifier(OID.portList), ports, 10000, 10, WalkMode.WithinSubtree, null, null);
                    Messenger.BulkWalk(snmpVersion, sw.endpoint, new OctetString(sw.community), null, new ObjectIdentifier(OID.bytesIn), bytesIn, 10000, 10, WalkMode.WithinSubtree, null, null);
                    Messenger.BulkWalk(snmpVersion, sw.endpoint, new OctetString(sw.community), null, new ObjectIdentifier(OID.bytesOut), bytesOut, 10000, 10, WalkMode.WithinSubtree, null, null);

                    List<PointData> points = SNMP.ProcessPorts(ports, bytesIn, bytesOut, sw, timeStamp);

                    using (var writeApi = influxClient.GetWriteApi())
                    {
                        writeApi.WritePoints(points, influxBucket, influxOrg);
                    }

                    stopwatch.Stop();
                    Thread.Sleep(30000 - (int)stopwatch.ElapsedMilliseconds);
                }
                catch (Lextm.SharpSnmpLib.Messaging.TimeoutException ex)
                {
                    Logging.WriteError($"Problem getting {sw.name} SNMP", ex.Message);
                    Console.WriteLine(ex.Message);
                }
                catch (Exception ex2)
                {
                    Logging.WriteError($"Problem sending {sw.name} data to influxDB", ex2.Message);
                    Console.WriteLine(ex2.Message);
                }
            }
        }
    }
}
