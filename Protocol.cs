using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO.Ports;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Sigma.Tool.UPS.DiagnosticData;
using Sigma.Tool.UPS.DiagnosticData.Config;
using static Sigma.Tool.UPS.DiagnosticData.ResponsePacket;


namespace Sigma.Tool.UPS.DiagnosticData
{
    /*
     Enumeration contenente tutta mappatura NomeComando -> codice 
     Since the commands fit within the range (0 to 255), 
     we store each value as a byte.
    */
    public enum ProtCommand : byte
    {
        UPSInfoRead = 0x00,
        OutputDataRead = 0x01,
        InputDataRead = 0x02,
        UPSStatusRead = 0x03,
        BatteryDataRead = 0x04,
        HistoryDataRead = 0x05,
        SchedulingRead = 0x06,
        EventListRead = 0x07,
        TimesOnBatteryRead = 0x08,
        BatteryTestSet = 0X0E
    }

    internal class Protocol
    {
        //Doc: https://learn.microsoft.com/it-it/dotnet/api/system.io.ports.serialport?view=dotnet-plat-ext-8.0
        SerialPort port = new SerialPort();
        SerialPortSettings settings;
        EventWaitHandle waitiforData = new EventWaitHandle(false, EventResetMode.ManualReset);
        static BlockingCollection<byte[]> buffer = new BlockingCollection<byte[]>(new ConcurrentQueue<byte[]>(), 10); /*10 is the capacity*/
        CancellationTokenSource cts = new CancellationTokenSource();
        //private object padlock = new object();



        public Protocol() {
            this.settings = SetDefaultConfiguration();
        }

        public Protocol(SerialPortSettings settings) {
            this.settings = settings;
        }

        private SerialPortSettings SetDefaultConfiguration()
        {

            var settings = new SerialPortSettings();
            settings.DataBits = 8;
            /*Ottiene o imposta il protocollo di controllo della parità:
             * esempio aggiunto un bit in modo che il numero totale di bit a valore "1"
             * sia sempre pari, utile per rilevare errori di trasmissioni nei dati.
             */
            settings.Parity = Parity.None;
            /*
             * Ottiene o imposta il numero standard dei bit di stop per byte:
             * dopo ogni byte di dati viene trasmesso un bit di stop per segnalare la fine del byte 
             * e permettere al ricevitore di sincronizzarsi per il prossimo byte.
             * 
             * ES:
             * 11011010 1 (byte di dati seguito da 1 bit di stop)
             */
            settings.StopBits = StopBits.One;
            //Frequenza(bit al secondo) 
            settings.BaudRate = 2400;
            //Porta di comunicazione (vedi gestione_periferiche -> USB COM5)
            settings.PortName = "COM4";
            return settings;
        }


        private void ConfigureSerialPort()
        {
            //Ottiene o imposta la lunghezza standard dei bit di dati per byte.
            port.DataBits = settings.DataBits;
            /*Ottiene o imposta il protocollo di controllo della parità:
            * esempio aggiunto un bit in modo che il numero totale di bit a valore "1"
             * sia sempre pari, utile per rilevare errori di trasmissioni nei dati.
            */
            port.Parity = settings.Parity;
            /*
             * Ottiene o imposta il numero standard dei bit di stop per byte:
             * dopo ogni byte di dati viene trasmesso un bit di stop per segnalare la fine del byte 
             * e permettere al ricevitore di sincronizzarsi per il prossimo byte.
             * 
             * ES:
             * 11011010 1 (byte di dati seguito da 1 bit di stop)
             */
            port.StopBits = settings.StopBits;
            //Frequenza(bit al secondo) 
            port.BaudRate = settings.BaudRate;
            //Porta di comunicazione (vedi gestione_periferiche -> USB COM5)
            port.PortName = settings.PortName;
        }

        /*
         * Configura parametri porta seriale e la apre.
         * Return True se la porta è stata aperta.
        */
        public bool Start()
        { 
            bool bRet=false;
            //SerialPortSettings settings = CustomUtility.ReadSerialPortSettingsFromFile();
            ConfigureSerialPort();
            port.DataReceived += PortDataReceived;
            port.Open();
            bRet = port.IsOpen;
            return bRet;    
        }


        private static void PrintArray(byte[] array, int length_to_print = 0)
        {
            length_to_print = length_to_print == 0 ? array.Length : length_to_print;
            int[] ints = Array.ConvertAll(array, Convert.ToInt32);
            //int[] bytesAsInts = array.Select(x => (int)x).ToArray();
            string content = "\n";
            for (int i = 0; i < length_to_print; i++)
            {
                content += $"\t{ints[i]}\n";
                //Console.WriteLine($"\t{ints[i]}");
            }
            Program.log.Info(content);
        }


        /*
            Listener evento ricezione dati:
                quando riceve un messaggio, verifica la validità
                ed in caso positivo triggera waitiforData.Set()
        */
        private void PortDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            //According to the protocol. whole packet has a maximum length of 256 bytes.
            byte[] data = new byte[256];
            int realLenght = port.Read(data, 0, data.Length);
            if (realLenght != 0)
            {
                Program.log.TraceDbg($"\n\tpacket received of length: {realLenght}");
                PrintArray(data, realLenght);
                byte[] subArray = new byte[realLenght];
                Array.Copy(data, 0, subArray, 0, realLenght);
                buffer.Add(subArray);               
            }
        }

        private bool ReadBuffer(ResponsePacket packet)
        {
            Program.log.TraceDbg("Reading buffer thread started");
            try
            {
                foreach (byte[] items in buffer.GetConsumingEnumerable(cts.Token))
                {
                    Program.log.TraceDbg("\tI'm reading.");
                    foreach (byte b in items)
                    {
                        ReadingState state = packet.AddByte(b);
                        if (state == ReadingState.End)
                        {
                            waitiforData.Set();
                            Program.log.TraceDbg("\nReading buffer thread ended successfully");
                            return true;
                        }
                    }
                }
            }
            catch (OperationCanceledException ex)          {

               // lock (padlock)
               // {
                    cts = new CancellationTokenSource(); // "Reset" the cancellation token source...
                //}
                Program.log.TraceDbg("\tReading buffer thread ended due to timeout");
                return false;
            }
            catch (Exception ex) {
                Program.log.TraceErr(ex.ToString());
                return false;
            }            
            return false;
        }
        
        //Richiesta invio messaggio
        public bool SendReceiveData(RequestPacket requestPacket, ResponsePacket responsePacket)
        {
            bool bRet = false;
            Task t = new Task(() => ReadBuffer(responsePacket), cts.Token);
            try
            {
                byte[] data = requestPacket.BufferOut();
                waitiforData.Reset();
                t.Start();
                port.Write(data, 0, data.Length);
                //Invia il messaggio
                //Attende 15 secondi
                //Blocca il thread, in attesa che venga sbloccato all'evento ricezione messaggio
                bRet = waitiforData.WaitOne(settings.Timeout);
                if (!bRet)
                    cts.Cancel(); //interrupt the reading thread
            }          
            catch (Exception ex)
            {
                Program.log.TraceException("SendReceiveData",ex);
            }
            Task.WaitAll(t);
            return bRet;
        }   
        
        /*
            Close the port
        */
        public bool Stop()
        {
            bool bRet;// = false;
            if (port.IsOpen)
            {
                port.Close();
                bRet = !port.IsOpen;
            }
            else 
            { 
                bRet = true;
            }
            return bRet;
        }
    }
}
