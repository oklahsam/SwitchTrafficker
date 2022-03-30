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
            string[] config;

            bool intervalTest = false;

            if (args.Length > 0)
                intervalTest = args[0].Trim().ToLower() == "intervaltest";

            string influxdb = string.Empty;
            string influxbucket = string.Empty;
            string influxtoken = string.Empty;
            string influxorg = string.Empty;
            string snmpversion = string.Empty;
            int interval = 30000;

            try
            {
                Console.WriteLine("Reading configuration file....");

                string configPath = Path.Combine(new string[] { ".", "SwitchTrafficker.conf" });
                config = File.ReadAllLines(configPath).Where(x => !x.StartsWith("#") && !string.IsNullOrWhiteSpace(x)).ToArray();

                string logPath = config.Where(x => x.StartsWith("logpath")).FirstOrDefault().Split('=',2)[1].Trim();

                Logging.InitializeLogs(logPath);

                if (config.Length <= 2)
                    throw new Exception("Invalid config file. Please edit SwitchTrafficker.conf", null);


                influxdb = config.Where(x => x.StartsWith("influxdb")).FirstOrDefault().Split('=', 2)[1].Trim();
                influxbucket = config.Where(x => x.StartsWith("influxbucket")).FirstOrDefault().Split('=', 2)[1].Trim();
                influxtoken = config.Where(x => x.StartsWith("influxtoken")).FirstOrDefault().Split('=', 2)[1].Trim();
                influxorg = config.Where(x => x.StartsWith("influxorg")).FirstOrDefault().Split('=', 2)[1].Trim();

                snmpversion = config.Where(x => x.StartsWith("snmpversion")).FirstOrDefault().Split('=', 2)[1].Trim();

                interval = int.Parse(config.Where(x => x.StartsWith("interval")).FirstOrDefault().Split('=', 2)[1].Trim());
            }
            catch (Exception ex)
            {
                Logging.WriteError("Problem reading config file", ex.Message);
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

                var swItem = new SwitchItem
                {
                    name = values[0].Trim(),
                    ip = values[1].Trim(),
                    community = values[2].Trim(),
                };

                if (values.Length >= 4)
                {
                    if (values[3].Trim().StartsWith('@'))
                        swItem.interval = int.Parse(values[3].Trim().Replace("@",""));
                    else
                        swItem.port = int.Parse(values[3].Trim());
                }

                if (values.Length == 5)
                    swItem.interval = int.Parse(values[4].Trim().Replace("@", ""));
                
                if (swItem.interval == 0)
                    swItem.interval = interval;

                switches.Add(swItem);
            }

            var influxClient = InfluxDBClientFactory.Create(influxdb, influxtoken);

            List<Task> tasks = new List<Task>();

            foreach (var sw in switches)
            {
                Console.WriteLine($"Starting task for {sw.name}....");
                tasks.Add(Task.Factory.StartNew(() => SwitchLoop(sw, influxClient, influxbucket, influxorg, snmpVersion, intervalTest)));
            }

            await Task.WhenAll(tasks);
        }

        static Task SwitchLoop(SwitchItem sw, InfluxDBClient influxClient, string influxBucket, string influxOrg, VersionCode snmpVersion, bool intervalTest)
        {
            bool loop = true;
            while (loop)
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                List<Variable> ports = new List<Variable>();
                List<Variable> bytesIn = new List<Variable>();
                List<Variable> bytesOut = new List<Variable>();
                List<Variable> desc = new List<Variable>();

                var timeStamp = DateTime.UtcNow;

                try
                {
                    Messenger.BulkWalk(snmpVersion, sw.endpoint, new OctetString(sw.community), null, new ObjectIdentifier(OID.portList), ports, 10000, 10, WalkMode.WithinSubtree, null, null);
                    Messenger.BulkWalk(snmpVersion, sw.endpoint, new OctetString(sw.community), null, new ObjectIdentifier(OID.bytesIn), bytesIn, 10000, 10, WalkMode.WithinSubtree, null, null);
                    Messenger.BulkWalk(snmpVersion, sw.endpoint, new OctetString(sw.community), null, new ObjectIdentifier(OID.bytesOut), bytesOut, 10000, 10, WalkMode.WithinSubtree, null, null);
                    Messenger.BulkWalk(snmpVersion, sw.endpoint, new OctetString(sw.community), null, new ObjectIdentifier(OID.portDesc), desc, 10000, 10, WalkMode.WithinSubtree, null, null);

                    List<PointData> points = SNMP.ProcessPorts(ports, bytesIn, bytesOut, desc, sw, timeStamp);

                    using (var writeApi = influxClient.GetWriteApi())
                    {
                        writeApi.WritePoints(points, influxBucket, influxOrg);
                    }

                    stopwatch.Stop();

                    int timeRemaining = sw.interval - (int)stopwatch.ElapsedMilliseconds;

                    if (intervalTest)
                    {
                        Console.WriteLine($"{sw.name}: {stopwatch.ElapsedMilliseconds} milliseconds");
                        loop = false;
                    }
                    else
                        Thread.Sleep(timeRemaining > 0 ? timeRemaining : 0);
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
            return Task.CompletedTask;
        }
    }
}
