using System;
using System.Threading.Tasks;

namespace FireWatch.Nodes
{
    public class EdwardsAegisNode
    {
        private readonly Network.UnivacAegisBridge _aegisBridge;
        private readonly EdwardsProtocolEncoder _encoder;

        public EdwardsAegisNode(Network.UnivacAegisBridge aegisBridge, EdwardsProtocolEncoder encoder)
        {
            _aegisBridge = aegisBridge;
            _encoder = encoder;
        }

        // Simulates receiving a raw byte stream from the Edwards RS-232/RS-485 interface
        public async Task ProcessEdwardsStreamAsync(byte[] rawPanelData)
        {
            Console.WriteLine("[EDWARDS NODE] Intercepted raw stream from Fire Panel.");

            // 1. Pass the raw bytes through your existing protocol encoder
            var decodedMessage = _encoder.Decode(rawPanelData);

            if (decodedMessage == null)
            {
                Console.WriteLine("[EDWARDS NODE] Invalid frame. Dropping packet.");
                return;
            }

            // 2. Map the Edwards state to the Univac Aegis severity matrix
            int aegisSeverity = MapEdwardsStateToAegis(decodedMessage.Status);

            // 3. Construct the Aegis Payload
            var payload = new Network.AegisPayload
            {
                SourceNode = "Edwards-Axis-AX-01",
                EventType = decodedMessage.EventType,
                SeverityLevel = aegisSeverity,
                RawProtocolData = BitConverter.ToString(rawPanelData)
            };

            // 4. Dispatch asynchronously to the Aegis Bridge
            await _aegisBridge.PushTelemetryAsync(payload);
        }

        private int MapEdwardsStateToAegis(string edwardsStatus)
        {
            // Translates Edwards proprietary states into a standardized 1-10 Aegis severity scale
            return edwardsStatus.ToUpper() switch
            {
                "NORMAL" => 0,
                "SUPERVISORY" => 4,
                "TROUBLE" => 5,
                "PRE-ALARM" => 8,
                "ALARM" => 10,
                _ => 1 // Unknown/Default
            };
        }
    }
}
