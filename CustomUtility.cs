using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using CsvHelper;
using Newtonsoft.Json;
using Sigma.Tool.UPS.DiagnosticData;
using Sigma.Tool.UPS.DiagnosticData.Config;

namespace Sigma.Tool.UPS.DiagnosticData
{
    internal class CustomUtility
    {
        public static int[] ConvertByteArrayToIntArray(byte[] byteArray)
        {
            // Ensure that the byte array length is divisible by 4 to form complete integers
            if (byteArray.Length % 4 != 0)
            {
                Program.log.Error("Byte array length must be divisible by 4.");
                throw new ArgumentException("Byte array length must be divisible by 4.");
            }

            // Create an int array to hold the converted values
            int[] intArray = new int[byteArray.Length / 4];

            // Iterate over the byte array, combining 4 consecutive bytes into integers
            for (int i = 0; i < byteArray.Length; i += 4)
            {
                intArray[i / 4] = BitConverter.ToInt32(byteArray, i);
            }

            return intArray;
        }
       

        internal static string GetProjectDirectory()
        {
            string workingDirectory = Environment.CurrentDirectory;
            string projectDirectory = Directory.GetParent(workingDirectory).Parent.FullName;
            return projectDirectory;
        }

        public static SerialPortSettings ReadSerialPortSettingsFromFile(string filename = "config.xml")
        {
            var filePath = GetProjectDirectory() + '/' + filename;
            SerialPortSettings settings = new SerialPortSettings();
            using (var fileStream = new FileStream(filePath, FileMode.Open))
            {
                using (var xmlReader = XmlReader.Create(fileStream))
                {
                    while (xmlReader.Read())
                    {
                        if (xmlReader.NodeType == XmlNodeType.Element)
                        {
                            switch (xmlReader.Name)
                            {
                                case "DataBits":
                                    settings.DataBits = int.Parse(xmlReader.ReadElementContentAsString());
                                    break;
                                case "Parity":
                                    settings.Parity = (Parity)Enum.Parse(typeof(Parity), xmlReader.ReadElementContentAsString());
                                    break;
                                case "StopBits":
                                    settings.StopBits = (StopBits)Enum.Parse(typeof(StopBits), xmlReader.ReadElementContentAsString());
                                    break;
                                case "BaudRate":
                                    settings.BaudRate = int.Parse(xmlReader.ReadElementContentAsString());
                                    break;
                                case "PortName":
                                    settings.PortName = xmlReader.ReadElementContentAsString();
                                    break;
                            }
                        }
                    }
                }
            }
            return settings;
        }

        public static bool writeToCsv(List<string> jsonObjects, string filepath)
        {
            //var filepath = GetProjectDirectory() + "/" + filename;
            var success = false;
            try
            {
                var dynamicObjects = new List<dynamic>();
                foreach (var jsonObject in jsonObjects)
                {
                    dynamic obj = JsonConvert.DeserializeObject<dynamic>(jsonObject);
                    dynamicObjects.Add(obj);
                }
                // Write CSV file
                var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    Delimiter = ";"
                };
                bool fileExists = File.Exists(filepath);
                using (var writer = new StreamWriter(filepath, append: fileExists))
                using (var csv = new CsvWriter(writer, config))//CultureInfo.InvariantCulture))
                {
                    if (!fileExists){
                        /**** HEADER ****/
                        // Write timestamp column
                        csv.WriteField("Timestamp");
                        foreach (var obj in dynamicObjects)
                        {
                            // Write header row
                            foreach (var property in obj.Properties())
                            {
                                csv.WriteField(property.Name);
                            }
                        }
                        /**** END_HEADER ****/
                        csv.NextRecord();
                    }
                    /**** CONTENT ****/
                    // Write records
                    csv.WriteField(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")); // Current timestamp
                    foreach (var obj in dynamicObjects)
                    {
                        foreach (var property in obj.Properties())
                        {
                            csv.WriteField(property.Value.ToString());
                        }
                    }
                    /**** END_CONTENT ****/
                    csv.NextRecord();
                }
                Program.log.Info("CSV file generated successfully.");
                success = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return success;
        }
    }

    /*
    class ConsoleLogger : TextWriter
    {
        private readonly TextWriter originalConsoleOut;
        private readonly StreamWriter fileWriter;

        public ConsoleLogger(string filename="log.txt")
        {
            var filePath = CustomUtility.GetProjectDirectory() + '/' + filename;
            originalConsoleOut = Console.Out;
            fileWriter = new StreamWriter(filePath);
            Console.SetOut(fileWriter);
        }

        public override Encoding Encoding => Encoding.UTF8;

        public override void WriteLine(string value)
        {
            originalConsoleOut.WriteLine(value); // Write to the original console as well
            fileWriter.WriteLine(value); // Write to the log file
            fileWriter.Flush(); // Ensure the written data is flushed to the file immediately
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Console.SetOut(originalConsoleOut); // Restore the original console
                fileWriter.Dispose(); // Close the StreamWriter
            }
            base.Dispose(disposing);
        }
    }*/
}
