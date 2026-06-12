using System;
using System.ServiceProcess;

namespace GenetecEdwardsBridge
{
    static class Program
    {
        static void Main()
        {
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new FireWatchService()
            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}
