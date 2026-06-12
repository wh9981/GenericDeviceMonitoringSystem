using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceSimulator.Simulator
{
    internal interface ISimulator
    {
        Guid Id { get; set; }
        string Name { get; set; }

        SimulatorRunState RunState { get; }
        PeerConnectionState ConnectionState { get; }

        event Action<ISimulator> StateChanged;
        event Action<string> LogReceived;

        Task StartAsync();
        Task StopAsync();
    }
}
