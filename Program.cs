﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using log4net.Config;
using log4net;
using Sigma.Tool.UPS.DiagnosticData.Config;
using Sigma.Utility.Logger;
using Sigma.Utility.Platform;



namespace Sigma.Tool.UPS.DiagnosticData
{
    internal class Program
    {
        public static Sigma.Utility.Logger.Logger log;

        static List<(RequestPacket, ResponsePacket)> initCommunicationPackets()
        {
            return new List<(RequestPacket, ResponsePacket)>
            {
               (new RequestPacket(ProtCommand.UPSInfoRead), new UPSInfoResponsePacket()),
               (new RequestPacket(ProtCommand.OutputDataRead), new OutputDataReader()),
               (new RequestPacket(ProtCommand.InputDataRead), new InputDataReader()),
               (new RequestPacket(ProtCommand.UPSStatusRead), new UPSStatusReader()),
               (new RequestPacket(ProtCommand.BatteryDataRead), new BatteryDataReader()),
               (new RequestPacket(ProtCommand.HistoryDataRead), new HistoryDataReader()),
               (new RequestPacket(ProtCommand.SchedulingRead), new SchedulingReader()),
             //  (new RequestPacket(ProtCommand.EventListRead), new EventListReader()),
               (new RequestPacket(ProtCommand.TimesOnBatteryRead), new TimesOnBatteryReader()),
               (new RequestPacket(ProtCommand.BatteryTestSet, new List<byte>(){0} ), new BatteryTest())
            };
        }

        static string GetOuputPath(PlatformDriver plt, string customPath = "")
        {
            string basePath = "";
            if(! string.IsNullOrEmpty(plt.PathPersistent))
                basePath = plt.PathPersistent;
            else
            {
                basePath = System.Environment.ExpandEnvironmentVariables("%appdata%");                
            }
            var fullPath = basePath + customPath;
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
                log.TraceDbg($"Created directory: {fullPath}");
            }
            return fullPath;
        }


        static void Main()
        {
            try
            {
                Sigma.Utility.Logger.Factory.Factory.BasicConfigure();
                log = Sigma.Utility.Logger.Factory.Factory.GetInstance("Sigma.Tool.UPS.DiagnosticData.log");
                log.TraceDbg("Start Log");
                Sigma.Utility.Platform.PlatformDriver plt = new Sigma.Utility.Platform.PlatformDriver();
                string filePathConfig = string.Format("{0}\\Device\\Sigma.Tool.UPS.DiagnosticData.Config.Configuration.xml", plt.PathAppData);
                Configuration myConfig = Sigma.Utility.Xml.XmlHandler<Configuration>.DeserializeFromXml(filePathConfig, null);
                var outputFolder = GetOuputPath(plt,myConfig.OutputFolder);
                Console.WriteLine(outputFolder);
                Log4NetConfigurator.configure(plt.PathLog + "\\UPS_console_log.txt");
                Protocol prot = new Protocol(myConfig.SerialPortSettings);
                var tupleList = initCommunicationPackets();
                HashSet<ProtCommand> unsuccesful_commands = new HashSet<ProtCommand>();
                List<string> parsedJsonObjects = new List<string>();
                //configura parametri porta seriale client e la apre
                if (prot.Start())
                {
                    foreach (var packetTuple in tupleList)
                    {
                        var requestPacket = packetTuple.Item1;
                        var responsePacket = packetTuple.Item2;
                        ProtCommand commandName = (ProtCommand)requestPacket.Cmd;
                        int attempt = 1;
                        bool success; //= false;
                        do
                        {
                            log.TraceDbg($"\nAttempt {attempt} elaborating command {requestPacket.Cmd}");
                            //Invia richiesta primo comando
                            success = prot.SendReceiveData(requestPacket, responsePacket);
                        } while (!success && attempt++ <= 3);
                        if (!success)
                        {
                            unsuccesful_commands.Add(commandName);
                            log.TraceDbg($"\tFailed to process command {commandName}");
                        }
                        else
                        {
                            string jsonObject = responsePacket.ToString();
                            Func<string, string> cleanForLog = (x) => x.Replace("{", "").Replace("}", "");

                            log.TraceDbg($"\nPacket content correctily parsed: {cleanForLog(jsonObject)}"); //jsonObject
                        }
                        //In case of failure, it will put an empty json object. 
                        //This will translate into fields of null values in the CSV record.
                        parsedJsonObjects.Add(responsePacket.ToString());
                    }
                    prot.Stop();
                    //All information are read, we can now translate list of JsonObjects into CSV file.
                    CustomUtility.writeToCsv(parsedJsonObjects, outputFolder + "UPS_Protocol.csv"); //"csv/UPS_Protocol.csv"
                }

                Sigma.Utility.Logger.Factory.Factory.FinalDispose();
                Console.Write("Please press any key to exit...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                log.TraceException("Main",ex);
            }
        }
    }
}

