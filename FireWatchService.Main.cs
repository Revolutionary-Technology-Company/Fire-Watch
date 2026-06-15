using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Genetec.Sdk;
// Inside your FireWatchService initialization block:

string aegisUrl = "https://api.revolutionary.technology/univac-aegis/ingest";

// Initialize the core tools
var protocolEncoder = new EdwardsProtocolEncoder();
var aegisBridge = new UnivacAegisBridge(aegisUrl);

// Instantiate the Edwards Node bridging the two
var edwardsNode = new EdwardsAegisNode(aegisBridge, protocolEncoder);

// When data arrives from FireWatchService.Network.cs, route it:
// await edwardsNode.ProcessEdwardsStreamAsync(incomingBytes);

namespace GenetecEdwardsBridge
{
    public partial class FireWatchService : ServiceBase
    {
        // Thread synchronization lock object
        private readonly object _lockObject = new object();
        
        // Configuration Properties
        private IConfigurationRoot _configuration;
        private GenetecSettings _genetecConfig;
        private EdwardsSettings _edwardsConfig;
        private Dictionary<string, CameraMapping> _lookupMap = new Dictionary<string, CameraMapping>();

        // System Services & Timers
        private Engine _genetecEngine;
        private System.Timers.Timer _heartbeatTimer;
        private Guid _kiwiFireEventGuid;

        public FireWatchService()
        {
            ServiceName = "GenetecEdwardsFireWatch";
            CanHandlePowerEvent = true;
            CanShutdown = true;
            CanStop = true;
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                // 1. Initialize Configuration and Hot-Swap Listeners
                InitializeAndWatchConfiguration();

                // 2. Instantiate and Initialize the Genetec SDK Engine
                _genetecEngine = new Engine();
                _genetecEngine.LoginManager.LoggedOn += OnGenetecLoggedOn;
                _genetecEngine.LoginManager.LoggedOff += OnGenetecLoggedOff;

                // 3. Initiate Non-Blocking Async Authentication to Genetec Server
                _genetecEngine.LoginManager.LogonAsync(
                    _genetecConfig.DirectoryServer, 
                    _genetecConfig.ServiceUser, 
                    _genetecConfig.ServicePassword
                );

                // 4. Initialize and Start the Supervision Watchdog Heartbeat
                int heartbeatMs = _edwardsConfig.HeartbeatIntervalSeconds * 1000;
                _heartbeatTimer = new System.Timers.Timer(heartbeatMs);
                _heartbeatTimer.Elapsed += OnHeartbeatTimerElapsed;
                _heartbeatTimer.Start();

                EventLog.WriteEntry(ServiceName, "FireWatch service initialized and running successfully.", EventLogEntryType.Information);
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry(ServiceName, $"Critical failure during service startup: {ex.Message}", EventLogEntryType.Error);
                throw; // Stop service execution immediately if initialization fails
            }
        }

        protected override void OnStop()
        {
            try
            {
                _heartbeatTimer?.Stop();

                if (_genetecEngine != null)
                {
                    if (_genetecEngine.LoginManager.IsLoggedOn)
                    {
                        _genetecEngine.ActionManager.UnregisterAction(EventType.VideoAnalyticsEvent, OnAnalyticsEventReceived);
                        _genetecEngine.LoginManager.Logoff();
                    }
                    _genetecEngine.Dispose();
                }
                
                EventLog.WriteEntry(ServiceName, "FireWatch service cleanly shut down and detached from system tasks.", EventLogEntryType.Information);
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry(ServiceName, $"Uncaught exception handling service termination routines: {ex.Message}", EventLogEntryType.Error);
            }
        }
    }
}
