using System;
using System.IO.Ports;

namespace SerialScope
{
    // Serial framing and control lines. The default (8N1, no flow control, DTR and RTS off)
    // suits ESP32 and Arduino boards and most USB-serial devices.
    internal sealed class PortConfig
    {
        public int DataBits = 8;
        public Parity Parity = Parity.None;
        public StopBits StopBits = StopBits.One;
        public Handshake Handshake = Handshake.None;
        public bool Dtr;
        public bool Rts;

        public static readonly int[] DataBitOptions = { 5, 6, 7, 8 };
        public static readonly Parity[] ParityOptions = { Parity.None, Parity.Odd, Parity.Even, Parity.Mark, Parity.Space };
        public static readonly StopBits[] StopBitOptions = { StopBits.One, StopBits.OnePointFive, StopBits.Two };
        public static readonly Handshake[] HandshakeOptions = { Handshake.None, Handshake.RequestToSend, Handshake.XOnXOff, Handshake.RequestToSendXOnXOff };

        public bool UsesRtsForFlowControl
        {
            get { return Handshake == Handshake.RequestToSend || Handshake == Handshake.RequestToSendXOnXOff; }
        }

        public bool IsDefault
        {
            get { return Summary == "8N1" && Handshake == Handshake.None && !Dtr && !Rts; }
        }

        // Short form shown on the button, e.g. "8N1" or "7E2"
        public string Summary
        {
            get { return DataBits + ParityLetter(Parity) + StopBitsText(StopBits); }
        }

        public static string ParityLetter(Parity p)
        {
            switch (p)
            {
                case Parity.Odd: return "O";
                case Parity.Even: return "E";
                case Parity.Mark: return "M";
                case Parity.Space: return "S";
                default: return "N";
            }
        }

        public static string StopBitsText(StopBits s)
        {
            return s == StopBits.Two ? "2" : s == StopBits.OnePointFive ? "1.5" : "1";
        }

        public static string HandshakeText(Handshake h)
        {
            switch (h)
            {
                case Handshake.RequestToSend: return "RTS/CTS (hardware)";
                case Handshake.XOnXOff: return "XON/XOFF (software)";
                case Handshake.RequestToSendXOnXOff: return "RTS/CTS + XON/XOFF";
                default: return "None";
            }
        }

        // Stored as e.g. "8N1;None;dtr=0;rts=0"
        public string Serialize()
        {
            return Summary + ";" + Handshake + ";dtr=" + (Dtr ? "1" : "0") + ";rts=" + (Rts ? "1" : "0");
        }

        public static PortConfig Parse(string text)
        {
            var c = new PortConfig();
            if (string.IsNullOrEmpty(text)) return c;
            string[] parts = text.Split(';');
            string frame = parts[0].Trim().ToUpperInvariant();
            if (frame.Length >= 3)
            {
                int bits;
                if (int.TryParse(frame.Substring(0, 1), out bits) && Array.IndexOf(DataBitOptions, bits) >= 0) c.DataBits = bits;
                switch (frame[1])
                {
                    case 'O': c.Parity = Parity.Odd; break;
                    case 'E': c.Parity = Parity.Even; break;
                    case 'M': c.Parity = Parity.Mark; break;
                    case 'S': c.Parity = Parity.Space; break;
                    default: c.Parity = Parity.None; break;
                }
                string stop = frame.Substring(2);
                c.StopBits = stop == "2" ? StopBits.Two : stop == "1.5" ? StopBits.OnePointFive : StopBits.One;
            }
            for (int i = 1; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                Handshake h;
                if (p.StartsWith("dtr=", StringComparison.OrdinalIgnoreCase)) c.Dtr = p.EndsWith("1");
                else if (p.StartsWith("rts=", StringComparison.OrdinalIgnoreCase)) c.Rts = p.EndsWith("1");
                else if (Enum.TryParse(p, true, out h)) c.Handshake = h;
            }
            return c;
        }

        public PortConfig Clone()
        {
            return (PortConfig)MemberwiseClone();
        }

        // Applies to a port (open or not); RTS can't be set by hand while RTS/CTS flow control owns it
        public void ApplyTo(SerialPort port)
        {
            port.DataBits = DataBits;
            port.Parity = Parity;
            port.StopBits = StopBits;
            port.Handshake = Handshake;
            port.DtrEnable = Dtr;
            if (!UsesRtsForFlowControl) port.RtsEnable = Rts;
        }
    }
}
