using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SwitchTrafficker
{
    public partial class Logging
    {
        private static string logFile = string.Empty;
        private static List<LogMessage> logMessages = new List<LogMessage>();

        public static void InitializeLogs(string _logPath)
        {
            try
            {
                logFile = Path.Combine(new string[] { _logPath, $"SwitchTrafficker.log" });

                if (!Directory.Exists(_logPath))
                {
                    Directory.CreateDirectory(_logPath);
                }

                Task.Run(() => StartLogs());

                Write("Startup logging", LogType.Information);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private static void StartLogs()
        {
            using (StreamWriter w = File.AppendText(logFile))
            {
                while (true)
                {
                    try
                    {
                        int count = logMessages.Count;
                        for (int i = 0; i < count; i++)
                        {
                            foreach (string line in logMessages[0].message.Split('\n'))
                            {
                                w.WriteLine($"{DateTime.Now} - {logMessages[0].type.ToString().PadRight(11, ' ')} - {line}");
                            }
                            w.Flush();
                            logMessages.Remove(logMessages[0]);
                        }
                        Thread.Sleep(500);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }
        }

        public static void WriteError(string warning, string error, [CallerMemberName] string caller = "", [CallerFilePath] string file = "")
        {
            Write(warning, LogType.Warning, caller, file);
            Write(error, LogType.Error, caller, file);
        }

        public static void WriteWarning(string message, [CallerMemberName] string caller = "", [CallerFilePath] string file = "")
        {
            Write(message, LogType.Warning, caller, file);
        }

        public static void WriteInfo(string message, [CallerMemberName] string caller = "", [CallerFilePath] string file = "")
        {
            Write(message, LogType.Information, caller, file);
        }

        /// <summary>
        /// Writes to the current log file
        /// </summary>
        /// <param name="message">Message for the log file entry</param>
        /// <param name="logType">Type of entry</param>
        public static void Write(string message, LogType logType, [CallerMemberName] string caller = "", [CallerFilePath] string file = "")
        {
            logMessages.Add(new LogMessage()
            {
                caller = FormatCaller(file, caller),
                message = message,
                type = logType
            });
            Console.WriteLine(message);
        }

        private static void Log(string message, LogType logType)
        {
            try
            {
                logMessages.Add(new LogMessage()
                {
                    message = message,
                    type = logType
                });
            }
            catch { }
        }

        private static string FormatCaller(string file, string caller)
        {
            file = Path.GetFileNameWithoutExtension(file).Replace(".xaml", "");
            caller = caller.Replace(".ctor", file);
            return $"{file}.{caller}";
        }
    }
}
