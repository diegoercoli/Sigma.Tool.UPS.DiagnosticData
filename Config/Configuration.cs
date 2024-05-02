using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sigma.Tool.UPS.DiagnosticData.Config
{
    public class Configuration
    {
        SerialPortSettings serialPortSettings = new SerialPortSettings();
        String outputFolder;

        public SerialPortSettings SerialPortSettings { get => serialPortSettings; set => serialPortSettings = value; }

        public string OutputFolder { get => outputFolder; set => outputFolder = value; }
    }

    public class SerialPortSettings
    {
        public int DataBits { get; set; }
        public Parity Parity { get; set; }
        public StopBits StopBits { get; set; }
        public int BaudRate { get; set; }
        public string PortName { get; set; }
        public int Timeout { get; set; }    
    }


}
