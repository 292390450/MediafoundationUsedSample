using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Rtsp.Messages
{
    /// <summary>
    /// Message wich represent data. ($ limited message)
    /// </summary>
    public class RtspData : RtspChunk
    {
       

        /// <summary>
        /// Logs the message to debug.
        /// </summary>
        public override void LogMessage(TraceLevel aLevel)
        {
            // Default value to debug
            if (aLevel == null)
                aLevel = TraceLevel.Info;
            // if the level is not logged directly return
            
            Trace.WriteLine(aLevel, "Data message");
            if (Data == null)
                Trace.WriteLine(aLevel, "Data : null");
            else
                Trace.WriteLine(aLevel+$"Data length :-{Data.Length}-");
        }

        public int Channel { get; set; }

        /// <summary>
        /// Clones this instance.
        /// <remarks>Listner is not cloned</remarks>
        /// </summary>
        /// <returns>a clone of this instance</returns>
        public override object Clone()
        {
            RtspData result = new RtspData();
            result.Channel = this.Channel;
            if (this.Data != null)
                result.Data = this.Data.Clone() as byte[];
            result.SourcePort = this.SourcePort;
            return result;
        }
    }
}
