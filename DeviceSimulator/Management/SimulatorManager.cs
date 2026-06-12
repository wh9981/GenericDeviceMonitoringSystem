using DeviceSimulator.Simulator;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceSimulator.Management
{
    internal class SimulatorManager
    {
        private readonly ConcurrentDictionary<Guid, ISimulator> _simulators = new ConcurrentDictionary<Guid, ISimulator>();

        public bool Add(ISimulator simulator)
        {
            return _simulators.TryAdd(simulator.Id, simulator);
        }

        public bool TryGet(Guid id, out ISimulator simulator)
        {
            return _simulators.TryGetValue(id, out simulator);
        }

        public bool Remove(Guid id, out ISimulator simulator)
        {
            return _simulators.TryRemove(id, out simulator);
        }
    }
}
