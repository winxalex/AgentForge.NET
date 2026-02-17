using System.Collections.Generic;
using System.Linq;

namespace Chat2Report.Services
{
    public static class ClientOrderService
    {
        private static readonly List<string> ClientIdOrder = new List<string>();
        private static readonly object Lock = new object();
        private static bool _isInitialized = false;

        public static void Initialize(IEnumerable<string> initialOrder)
        {
            lock (Lock)
            {
                if (!_isInitialized && initialOrder.Any())
                {
                    ClientIdOrder.AddRange(initialOrder);
                    _isInitialized = true;
                }
            }
        }

        public static List<string> GetOrderedIds()
        {
            lock (Lock)
            {
                return new List<string>(ClientIdOrder);
            }
        }

        public static void MoveToEnd(string clientId)
        {
            lock (Lock)
            {
                if (ClientIdOrder.Remove(clientId))
                {
                    ClientIdOrder.Add(clientId);
                }
            }
        }
    }
}
