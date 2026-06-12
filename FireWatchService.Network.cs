using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Timers;
using Genetec.Sdk;
using Genetec.Sdk.Events;

namespace GenetecEdwardsBridge
{
    public partial class FireWatchService
    {
        private void OnGenetecLoggedOn(object sender, LoggedOnEventArgs e)
        {
            // Subscribe natively to system-wide Video Analytics Event hooks
            _genetecEngine.ActionManager.RegisterAction(EventType.VideoAnalyticsEvent, OnAnalyticsEventReceived);
            EventLog.WriteEntry(ServiceName, "Successfully established authentication bridge with Genetec Directory Server.", EventLogEntryType.Information);
        }

        private void OnGenetecLoggedOff(object sender, LoggedOffEventArgs e)
        {
            EventLog.WriteEntry(ServiceName, "Warning: System disconnected from Genetec Directory. Re-authenticating context automatically...", EventLogEntryType.Warning);
        }

        private void OnAnalyticsEventReceived(object sender, ActionReceivedEventArgs e)
        {
            if (e.Event is VideoAnalyticsEvent analyticsEvent)
            {
                // Fast checking filtering optimization logic to reject non-fire data immediately
                if (analyticsEvent.AnalyticsTypeGuid == _kiwiFireEventGuid)
                {
                    string searchGuid = analyticsEvent.SourceGuid.ToString().ToLower().Trim();

                    // High-performance thread-safe route lookup mapping execution block
                    lock (_lockObject)
                    {
                        if (_lookupMap.TryGetValue(searchGuid, out CameraMapping targetLocation))
                        {
                            SendAlertToEdwards(
                                targetLocation.EdwardsNode, 
                                targetLocation.EdwardsZone, 
                                targetLocation.PhysicalRoom, 
                                analyticsEvent.Timestamp
                            );
                        }
                        else
                        {
                            EventLog.WriteEntry(ServiceName, $"Fire alert dropped! Received alert for Camera GUID '{searchGuid}', but it has no mapped node/zone layout path defined inside appsettings.json.", EventLogEntryType.Warning);
                        }
                    }
                }
            }
        }

        private void SendAlertToEdwards(string node, string zone, string physicalRoom, DateTime timestamp)
        {
            try
            {
                // Encode into the precise byte payload format expected by Edwards FireWorks Text Driver
                byte[] rawPacketData = EdwardsProtocolEncoder.EncodeAlarmPayload(node, zone, physicalRoom, timestamp);

                using (TcpClient client = new TcpClient())
                {
                    var connectResult = client.ConnectAsync(_edwardsConfig.ReceiverIp, _edwardsConfig.ReceiverPort);
                    if (!connectResult.Wait(TimeSpan.FromSeconds(3)))
                    {
                        throw new TimeoutException("Network timeout window expired while attempting connection to FireWorks host.");
                    }

                    using (NetworkStream stream = client.GetStream())
                    {
                        stream.Write(rawPacketData, 0, rawPacketData.Length);
                        stream.Flush();
                    }
                }
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry(ServiceName, 
                    $"CRITICAL TRANSMISSION FAILURE: Could not forward fire alert to Edwards platform at {_edwardsConfig?.ReceiverIp}:{_edwardsConfig?.ReceiverPort}.\n" +
                    $"Target Layout Destination: Node {node}, Zone {zone} ({physicalRoom}).\n" +
                    $"Exception Trace: {ex.Message}", 
                    EventLogEntryType.Error);
            }
        }

        private void OnHeartbeatTimerElapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                byte[] heartbeatPacket = EdwardsProtocolEncoder.EncodeHeartbeatPayload();

                using (TcpClient client = new TcpClient())
                {
                    var connectResult = client.ConnectAsync(_edwardsConfig.ReceiverIp, _edwardsConfig.ReceiverPort);
                    if (connectResult.Wait(TimeSpan.FromSeconds(2)))
                    {
                        using (NetworkStream stream = client.GetStream())
                        {
                            stream.Write(heartbeatPacket, 0, heartbeatPacket.Length);
                            stream.Flush();
                        }
                    }
                }
            }
            catch
            {
                // Fail silently to prevent cyclic error log flood storms from building inside Windows Event logs
            }
        }
    }
}
