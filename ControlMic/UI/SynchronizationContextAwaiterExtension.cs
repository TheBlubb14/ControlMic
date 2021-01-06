using System.Threading;

namespace ControlMic.UI
{
    public static class SynchronizationContextAwaiterExtension
    {
        public static SynchronizationContextAwaiter GetAwaiter(this SynchronizationContext context)
        {
            return new SynchronizationContextAwaiter(context);
        }
    }
}
