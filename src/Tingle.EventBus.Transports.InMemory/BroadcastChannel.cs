using System.Threading.Channels;

namespace Tingle.EventBus.Transports.InMemory
{
    class BroadcastChannel<T> : Channel<T>
    {
        public BroadcastChannel()
        {
            Writer = new BroadcastChannelWriter<T>();
        }
    }
}
