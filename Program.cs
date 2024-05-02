using Newtonsoft.Json;
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



namespace Sigma.Tool.UPS.DiagnosticData
{
    internal class Program
    {
        internal static readonly ILog log = LogManager.GetLogger(typeof(Program));

        static List<(RequestPacket, ResponsePacket)> initCommunicationPackets(){
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

        static void Main()
        {
            /*
            Sigma.Utility.Logger.Factory.Factory.BasicConfigure();
            Sigma.Utility.Logger.Logger myLog =  Sigma.Utility.Logger.Factory.Factory.GetInstance("Sigma.Tool.UPS.DiagnosticData.log");
            myLog.TraceDbg("Start Log");
            */
           // myLog.TraceDbgFormat();
            Sigma.Utility.Platform.PlatformDriver plt = new Sigma.Utility.Platform.PlatformDriver();
            string filePathConfig = string.Format("{0}\\Device\\Sigma.Tool.UPS.DiagnosticData.Config.Configuration.xml", plt.PathAppData);
            Configuration myConfig = Sigma.Utility.Xml.XmlHandler<Configuration>.DeserializeFromXml(filePathConfig, null);
            myConfig.OutputFolder = plt.PathAppData+ myConfig.OutputFolder;
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
                        log.Info($"\nAttempt {attempt} elaborating command {requestPacket.Cmd}");
                        //Invia richiesta primo comando
                        success = prot.SendReceiveData(requestPacket, responsePacket);
                    } while (!success && attempt++ <= 3);
                    if (!success)
                    {
                        unsuccesful_commands.Add(commandName);
                        log.Error($"\tFailed to process command {commandName}");
                    }
                    else
                    {
                        string jsonObject = responsePacket.ToString();
                        log.Info($"\nPacket content correctily parsed: {jsonObject}");
                    }
                    //In case of failure, it will put an empty json object. 
                    //This will translate into fields of null values in the CSV record.
                    parsedJsonObjects.Add(responsePacket.ToString());
                }
                prot.Stop();
                //All information are read, we can now translate list of JsonObjects into CSV file.
                CustomUtility.writeToCsv(parsedJsonObjects, myConfig.OutputFolder + "UPS_Protocol.csv"); //"csv/UPS_Protocol.csv"
            }
            Console.ReadKey();
        }
    }
}
