using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sigma.Tool.UPS.DiagnosticData
{
    internal class RequestPacket : Packet
    {
        private List<Byte> parameters;

        public RequestPacket(ProtCommand cmd) : base(cmd) { 
            // empty list
            this.parameters = new List<byte>();
            this.targetLength = this.SizeCmd + this.SizeChecksum;
        }

        public RequestPacket(ProtCommand cmd, List<byte> parameters) : base(cmd)
        {
            this.parameters = parameters;
            this.targetLength = this.SizeCmd + this.SizeChecksum + parameters.Count;
        }

        public override List<byte> Data
        {
            get
            {
                if (this.data.Count > 0)
                    return this.data;
                List<byte> dataOut = new List<byte>();
                //dataOut.Add(Cmd);
                if(parameters.Count > 0)
                    dataOut.AddRange(parameters.ToArray());
                this.data = dataOut;
                return dataOut;
            }
        }
    }
}
