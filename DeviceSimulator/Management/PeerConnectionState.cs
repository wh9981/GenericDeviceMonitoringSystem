using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceSimulator.Simulator
{
    internal enum PeerConnectionState
    {
        NotApplicable,
        Waiting,
        Connected,
        Disconnected,
        TimedOut,
        Faulted
    }
}
