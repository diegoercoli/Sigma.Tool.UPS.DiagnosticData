using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Sigma.Tool.UPS.DiagnosticData;

namespace Sigma.Tool.UPS.DiagnosticData
{
    internal class UnfilledPacketException : ApplicationException
    {
        public UnfilledPacketException() 
        {
            Program.log.Error("Packet is not complete!");
        }
    }
    

    internal class OverflowPacketException: ApplicationException
    {
        public OverflowPacketException()
        {
            Program.log.Error("Packet is alreay full!");
        }
    }

    public class DataIntegrityException : ApplicationException
    {
        public DataIntegrityException()
        {
            Program.log.Error("Checksum is not respected! Potential issue related to data integrity.");
        }
    }


    internal abstract class Packet
    {
        protected int targetLength;
        private byte checksum;
        protected byte? cmdCode;
        protected const byte STX = 0x02;
        public const int wordSize = 2; // Size of command in bytes

        //field data
        protected List<Byte> data;
    
        public Packet() {
            cmdCode = null;
            targetLength = 0;
            data = new List<Byte>();
        }

        public Packet(ProtCommand cmd) {
            cmdCode = (byte) cmd;
            checksum = 0;
            targetLength = 0;
            data = new List<Byte>();
        }

        /*
         Data comprises all the content of the Packet data (referring to the field data in the Packet format)
         except for the command code.
        */
        public abstract List<byte> Data {
            get;
        }

        public byte Cmd
        {
            get { return (byte)cmdCode; }
        }


        public int Length
        {
            get {
                int setChecksum = SizeChecksum; //((int)checksum != 0) ? 1 : 0; ;
                int setCmd = ((int)cmdCode != null) ? SizeCmd : 0; ;
                int l = data.Count + setChecksum + setCmd;
                return l;
                /*
                int l = 0;
                if (IsFull)
                {
                    l = data.Count + SizeChecksum + 1;
                }
                return l;*/ }
            //set { targetLength = value; }
        }


        public int TargetLength
        { get { return targetLength; } 
          set { targetLength= value; }
        }


        public byte Checksum
        { get {
                if (!IsFull)
                    return 0; //throw new UnfilledPacketException();
                if (checksum != 0 )
                    return checksum;
                checksum = ComputingChecksum();
                return checksum; 
            }
        }

        public bool CompareChecksum(byte targetChecksum)
        {
           // bool isEqual;
            if ( targetChecksum == Checksum)
                return true;
            else
                throw new DataIntegrityException();
            //return isEqual;
        }

        private byte ComputingChecksum()
        {
          //  Console.WriteLine("-----CHECKSUM--------!");
          //  Console.WriteLine("Dati \n" + BitConverter.ToString(data.ToArray()).Replace("-", " ") + "\n\n");

            byte crcRet = (byte) TargetLength;
            crcRet += (byte) cmdCode;
            foreach (var item in Data)
            {
                crcRet += item;
            }
            byte checkByte = (byte)(crcRet % 256);
            return checkByte;
        }

        protected int SizeChecksum
        {
            get
            {
                return 1; // BitConverter.GetBytes(checksum).Length;
            }
        }

        protected int SizeCmd
        {
            get
            {
                return 1;// BitConverter.GetBytes((byte)cmdCode).Length;
            }
        }


        public bool IsFull
        {
            get { return targetLength == Length; } //1 stands for cmdCode
        }

        public byte[] BufferOut()
        {
            List<byte> dataOut = new List<byte>();
            dataOut.Add(STX);
            dataOut.Add((byte)Length);
            dataOut.Add((byte)cmdCode);
            dataOut.AddRange(Data);
            dataOut.Add(Checksum);
            return dataOut.ToArray();
        }       
    }
}
