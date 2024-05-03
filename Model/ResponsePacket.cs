using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace Sigma.Tool.UPS.DiagnosticData
{
    internal class ResponsePacket : Packet
    {
        public enum ReadingState
        {
            Start=0, 
            Header=1, //currenty reading metadata such as data length
            Data=2, //data after the cmd command
            End=3  //set after getting the checksum
        }

        private ReadingState readingState;

        public ResponsePacket():base() { 
            readingState = ReadingState.Start; 
        }
        //public ResponsePacket(ProtCommand cmd) : base(cmd) { }
        // Use reflection to get all the fields of the 'leaf' class
        private FieldInfo[] ReadChildFields()
        {
            FieldInfo[] fields =  GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            return fields;
        }

        public int GetDataSizeInByte()
        {
            FieldInfo[] fields = ReadChildFields();
            int sum = 0;
            foreach (var field in fields)
            {
                Console.WriteLine($"{field.FieldType.ToString()}-{field.FieldType.BaseType.Name}");
                int sizeInByte = GetFieldSize(field);
                string type = field.FieldType.Name.Split('.').Last();
                Console.WriteLine($"Field: {field.Name}, Type: {type}, Size: {sizeInByte} bytes");
                sum += sizeInByte;//Marshal.SizeOf(sizeInByte);
            }
            return sum;
        }

        public override List<byte> Data
        {
            get
            {
                return this.data;
            }
        }

        public ReadingState AddByte(byte rawbyte)
        {
            switch (readingState)
            {
                case ReadingState.Start:
                    if (rawbyte == STX)
                        readingState = ReadingState.Header;
                    else
                    {
                        Program.log.TraceErr("$\"Expected code {STX} at the beginning of the command, but received instead {rawbyte}.\"");
                        throw new ArgumentException($"Expected code {STX} at the beginning of the command, " +
                                                   $"but received instead {rawbyte}.");                    }
                       
                    break;
                case ReadingState.Header:
                    //data length is expected as first field
                    if (TargetLength == 0)
                        targetLength = (int)rawbyte;
                    //command code is expected as second field
                    else if (cmdCode == null)
                    {
                        cmdCode = rawbyte;
                        //data.Add(rawbyte);
                        readingState = ReadingState.Data;
                    }
                    break;
                case ReadingState.Data:
                    if (IsFull) //2 because data doesn't contain both the command and the checksum
                    {
                        var targetChecksum = rawbyte;   
                        CompareChecksum(targetChecksum); //raise an exception in case of problems
                        this.ParseBytes();
                        readingState = ReadingState.End;
                    } 
                    else
                        data.Add(rawbyte);
                    break;
                case ReadingState.End:
                    throw new OverflowPacketException();
            }
            return readingState;
        }

        private int GetFieldSize(FieldInfo field)
        {
            int sizeInByte;
            if (field.FieldType.IsArray)
                sizeInByte = (field.GetValue(this) as Array).Length;
            //(field.GetValue(new UPSInfoResponsePacket()) as Array).Length;
            else if (field.FieldType.BaseType.Name == "Enum")
                sizeInByte = 1;
            else
                sizeInByte = Marshal.SizeOf(field.FieldType);
            return sizeInByte;
        }

        protected int decodeWord(byte[] word)
        {
            if (word.Length != wordSize)
            {
                Program.log.TraceErr($"Length of word {word.Length} is different from the expected word size {wordSize}");
                throw new ArgumentException($"Length of word {word.Length} is different from the expected word size {wordSize}");
            }
            if (wordSize != 2)
            {
               Program.log.TraceDbg("Method decodeWord can only deal with word of length 2.");
               throw new NotSupportedException("Method decodeWord can only deal with word of length 2.");
            }
            int value = (word[1] << 8) | word[0];
            return value;///10;
        }

        private bool isEnum(FieldInfo field)
        {
            string specificType = field.FieldType.BaseType.Name;
            return specificType == "Enum";
        }

        public void ParseBytes()
        {
            if (!IsFull)
                throw new UnfilledPacketException();
            byte[] rawData = this.Data.ToArray();
            FieldInfo[] fields = ReadChildFields();
            int lowerIndex = 0;
            Program.log.TraceDbg("\n\nGetting fields to fill using Reflection:\n");
            foreach (var field in fields)
            {
                int sizeInByte = GetFieldSize(field);
                string type = field.FieldType.Name.Split('.').Last();
                Program.log.TraceDbg($"\tField: {field.Name}, Type: {type}, Size: {sizeInByte} bytes");                
                byte[] subarray = new byte[sizeInByte];
                Array.Copy(rawData, lowerIndex, subarray, 0, sizeInByte);
                lowerIndex += sizeInByte;
                if (isEnum(field))
                {
                    field.SetValue(this, subarray[0]);
                    continue;
                }
                switch(type)
                {
                    case "Int16":
                        short shortValue = BitConverter.ToInt16(subarray, 0);
                        field.SetValue(this, shortValue);
                        break;
                    case "Int32":
                        int intValue = BitConverter.ToInt32(subarray, 0);
                        field.SetValue(this, intValue);
                        break;
                    case "Int64":
                        long longValue = BitConverter.ToInt64(subarray, 0); //.ToInt32(subarray, 0);
                        field.SetValue(this, longValue);
                        break;
                    case "Char":
                        field.SetValue(this, (char)subarray[0]);
                        break;
                    case "Char[]":
                        char[] asciiChars = Encoding.ASCII.GetString(subarray).ToCharArray();
                        field.SetValue(this, asciiChars);
                        break;
                    case "Byte":
                        field.SetValue(this, subarray[0]);
                        break;
                    case "Byte[]":
                        field.SetValue(this, subarray);
                        break;
                    case "SByte":
                        field.SetValue(this, (sbyte)subarray[0]);
                        break;
                }                
            }
        }

        public virtual string ToString()
        {
            List<JProperty> properties = new List<JProperty>();
            Func<string, string> capitalizeFirstLetter = str => char.ToUpperInvariant(str[0]) + str.Substring(1);
            foreach (var field in ReadChildFields())
            {
                var name = this.cmdCode + "_" + field.Name;
                PropertyInfo propertyInfo = this.GetType().GetProperty(capitalizeFirstLetter(field.Name));
                if (propertyInfo != null)
                {
                    var value = propertyInfo.GetValue(this);
                    properties.Add(new JProperty(name, value));
                }
                else if (isEnum(field))
                {
                    var target_value = field.GetValue(this);
                    properties.Add(new JProperty(name, target_value.ToString()));
                }
                else
                {
                    var value = field.GetValue(this);
                    properties.Add(new JProperty(name, value));
                }
                /*
                if (field.FieldType.Name == "Byte[]" & this.GetFieldSize(field) == wordSize)                    
                    properties.Add(new JProperty(name, decodeWord((byte[])value)));
                else
                    properties.Add(new JProperty(name, value));*/
            }
            JObject obj = new JObject(properties);
            return obj.ToString(Formatting.Indented);
        }
    }
}
