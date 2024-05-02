using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using static Sigma.Tool.UPS.DiagnosticData.UPSInfoResponsePacket;
namespace Sigma.Tool.UPS.DiagnosticData
{
    internal class UPSInfoResponsePacket : ResponsePacket
    {
        public enum Model : byte
        {
            HFLine = 1,
            ECONetwork = 2,
            ECO308_311 = 3,
            HFLine_2 = 4,
            HFMillennium = 5,
            HFTopLine_AEROD = 6,
            HFTopLine_2 = 7,
            ECONetwork_5xx = 8,
            ECO305_Innik550 = 9,
            ECOInteractiveSX_Innik770_1100 = 10,
            AllyHF800_1600_DPRO800_1600_BITWICE800 = 11,
            AllyHF1000_2000_DPRO1000_2000_BITWICE1000 = 12,
            AllyHF1250_2500_DPRO1250_2500_BITWICE1250 = 13,
            HFMegaline = 14,
            HFMegaline_2 = 15,
            WHAD800 = 17,
            WHAD1000 = 18,
            DHEA1000 = 20,
            DHEA1500 = 21,
        }
        private byte model;
        private byte configuration;
        private byte[] maxActivePower = new byte[wordSize]; 
        private byte firmwareVersion;
        private byte firmwareSubVersion;
        private char[] SerialNumber = new char[12];

        public UPSInfoResponsePacket() : base() { }

        public string Firmware
        {
            get { return firmwareVersion.ToString() + "." + firmwareSubVersion.ToString(); }
        }

        public int ActivePower
        {
            get => decodeWord(maxActivePower);
        }

        public override string ToString()
        {
            int activePower = this.decodeWord(maxActivePower);
            JObject jsonObject = new JObject(
            new JProperty($"{this.cmdCode}_Model",  ((Model) model).ToString()),
            new JProperty($"{this.cmdCode}_Configuration", configuration),
            new JProperty($"{this.cmdCode}_MaxActivePower", ActivePower),
            new JProperty($"{this.cmdCode}_Firmware", Firmware),
            new JProperty($"{this.cmdCode}_SerialNumber", new string(SerialNumber))
            // Add more properties as needed
            );
            return jsonObject.ToString(Formatting.Indented);
        }
    }

    internal class OutputDataReader : ResponsePacket
    {        
        private short activePower;
        private short voltage;
        private short current;
        private short peakCurrent;
        public OutputDataReader() : base() { }

        public float Current
        {
            get { return ((float)current /10); }
        }
        public float PeakCurrent
        {
            get { return ((float)peakCurrent / 10); }
        }

    }

    internal class InputDataReader : ResponsePacket
    {
        private short activePower;
        private short voltage;
        private short current;
        private short peakCurrent;

        public InputDataReader() : base() { }

        public float Current
        {
            get { return ((float)current / 10); }

        }
        public float PeakCurrent
        {
            get { return ((float)peakCurrent / 10); }

        }
    }

    internal class UPSStatusReader : ResponsePacket
    {
        public enum Status : byte
        {
            RunningOnMainsPower = 0,
            RunningOnBatteryPower = 1,
            BatteryReserve = 2,
            BypassEngaged = 3,
            ManualBypassEngaged = 4
        }

        public enum Fault : byte
        {
            AllRight = 0,
            Overload = 1,
            Overheat = 2,
            HardwareFault = 3,
            BatteryChargerFailure = 4,
            ReplaceBatteries = 5
        }

        private Status status;
        private Fault fault;
        private sbyte temperature; // Note: sbyte can represent -128 to 127, suitable for temperature adjustments

        public UPSStatusReader() : base() { }

        public int Temperature
        {
            get => (int)temperature + 128;
        }
    }

    internal class BatteryDataReader : ResponsePacket
    {
        private byte[] actualValue = new byte[wordSize]; // (V * 10)
        private byte[] reserveThreshold = new byte[wordSize]; // (V * 10)
        private byte[] exhaustThreshold = new byte[wordSize]; // (V * 10)

        public BatteryDataReader() : base() { }

        public float ActualValue
        {
            get { return (float) decodeWord((byte[])actualValue) / 10; }

        }
        public float ReserveThreshold
        {
            get { return (float)decodeWord((byte[])reserveThreshold) / 10; }

        }
        public float ExhaustThreshold
        {
            get { return (float)decodeWord((byte[])exhaustThreshold) / 10; }

        }
    }

    internal class HistoryDataReader : ResponsePacket
    {
        private int upsTotalRunTime; // (s)
        private int inverterTotalRunTime; // (s)
        private byte[] inverterInterventions = new byte[wordSize];
        private byte[] batteryFullDischarges = new byte[wordSize];
        private short stabiliserOrBypassInterventions; // -2 = not available
        private short overheatings; // -2 = not available

        public HistoryDataReader() : base() { }

        public int InverterInterventions
        {
            get => decodeWord(inverterInterventions);
        }

        public int BatteryFullDischarges
        {
            get => decodeWord(batteryFullDischarges);
        }
    }

    internal class SchedulingReader : ResponsePacket
    {
        private int remainingTimeToShutdown; // (s) -1 = no shutdown
        private int programmedTimeToRestart; // (s) -1 = no restart

        public SchedulingReader() : base() { }
    }

    /*
    internal class EventListReader : ResponsePacket
    {
        public enum EventCode : byte
        {
            OddTurnOff = 1,
            MemoryError = 2,
            BatteryLimit = 3,
            BatteryCharger = 4,
            Overload = 5,
            LongOverload = 6,
            NeutralWrong = 7,
            NeutralWrongWhileRunning = 8,
            ModulesNumber = 9,
            ProgrammedBatteryTimeExpired = 10,
            ProgrammedReserveTimeExpired = 11,
            EarthFault = 12,
            LoadWaiting = 13,
            HVBusRunaway = 14,
            OutputDCLevel = 15,
            BadWiring = 16,
            HardwareFaultUnknown = 17,
            HardwareFaultInverter = 18,
            HardwareFaultPFC = 19,
            HardwareFaultInverterPFC = 20,
            HardwareFaultBooster = 21,
            HardwareFaultInverterBooster = 22,
            HardwareFaultPFCBooster = 23,
            HardwareFaultInverterPFCBooster = 24,
            HardwareFaultOverheat = 25,
            HardwareFaultInverterOverheat = 26,
            HardwareFaultPFCOverheat = 27,
            HardwareFaultInverterPFCOverheat = 28,
            HardwareFaultBoosterOverheat = 29,
            HardwareFaultInverterBoosterOverheat = 30,
            HardwareFaultPFCBoosterOverheat = 31,
            HardwareFaultInverterPFCBoosterOverheat = 32,
            HardwareFaultBatteryCharger = 33,
            HardwareFaultOverheatBatteryCharger = 34,
            HardwareFaultOutputPlugRemoved = 35
        }

        private byte eventAbsoluteCounter;
        private List<EventCode> eventList;

        public EventListReader() : base() { }
    }*/

    internal class TimesOnBatteryReader : ResponsePacket
    {
        private short maxTimeOnBattery; // (s) 0 = not purposedly limited, -2 = not available
        private short maxTimeAfterBatteryReserve; // (s) 0 = not purposedly limited, -2 = not available
        private byte autorestart; 

        public TimesOnBatteryReader() : base() { }
    }

    internal class BatteryTest : ResponsePacket
    {
        public enum Status: byte
        {
            GenericOk = 0,
            BatteryCharge20 = 1,
            BatteryCharge40 = 2,
            BatteryCharge60 = 3,
            BatteryCharge80 = 4,
            BatteryCharge100 = 5,
            BatteriesMustBeReplaced = 254,
            TestImpossible = 255
        }

        private Status status;

        public BatteryTest() : base() { }

    }
}

