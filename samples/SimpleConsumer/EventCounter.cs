using System.Threading;

namespace SimpleConsumer;

public class EventCounter
{
    private int count = 0;

    public int Count => count;

    public void Consumed()
    {
        Interlocked.Increment(ref count);
    }
}
