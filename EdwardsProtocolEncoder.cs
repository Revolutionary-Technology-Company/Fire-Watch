using System;
using System.Text;

namespace GenetecEdwardsBridge
{
    public static class EdwardsProtocolEncoder
    {
        // Fundamental ASCII Control Characters used by life-safety terminal drivers
        private const char STX = (char)0x02; // Start of Text
        private const char ETX = (char)0x03; // End of Text
        private const char CR  = (char)0x0D; // Carriage Return

        /// <summary>
        /// Formats a visual fire alert into a strict, delimited message frame for FireWorks.
        /// </summary>
        public static byte[] EncodeAlarmPayload(string node, string zone, string physicalRoom, DateTime timestamp)
        {
            // Sanitize string inputs to prevent comma injection or illegal delimiter spacing
            string cleanNode = SanitizeString(node);
            string cleanZone = SanitizeString(zone);
            string cleanRoom = SanitizeString(physicalRoom);
            string isoTime   = timestamp.ToString("yyyy-MM-dd HH:mm:ss");

            // Standard FireWorks External Systems Text Pattern:
            // STX + EVENT_TYPE , NODE_ID , ZONE_ID , DESC_TEXT , TIMESTAMP + ETX + CR
            StringBuilder frame = new StringBuilder();
            frame.Append(STX);
            frame.Append("ALARM,");
            frame.Append($"{cleanNode},");
            frame.Append($"{cleanZone},");
            frame.Append($"VISUAL FIRE DETECTED IN {cleanRoom},");
            frame.Append(isoTime);
            frame.Append(ETX);
            frame.Append(CR);

            return Encoding.ASCII.GetBytes(frame.ToString());
        }

        /// <summary>
        /// Formats a watchdog/heartbeat frame. If FireWorks does not see this pattern 
        /// within its configured timeout window, it throws a "System Trouble" alert.
        /// </summary>
        public static byte[] EncodeHeartbeatPayload()
        {
            StringBuilder frame = new StringBuilder();
            frame.Append(STX);
            // SYSTEM STATUS , NODE_IDENTIFIER , CONDITION
            frame.Append("STATUS,BRIDGE,NORMAL,HEARTBEAT OK");
            frame.Append(ETX);
            frame.Append(CR);

            return Encoding.ASCII.GetBytes(frame.ToString());
        }

        /// <summary>
        /// Prevents delimiters from breaking the FireWorks parsing engine.
        /// </summary>
        private static string SanitizeString(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "UNKNOWN";

            // Strip out raw control characters, commas, and pipe structures
            string sanitized = input.Replace(",", " ")
                                    .Replace("|", " ")
                                    .Replace("\x02", "")
                                    .Replace("\x03", "")
                                    .Replace("\x0D", "")
                                    .Trim();

            // Enforce upper case if the Edwards database requires all zone configurations in caps
            return sanitized.ToUpperInvariant();
        }
    }
}
