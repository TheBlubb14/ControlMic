using System.Threading;

namespace ControlMic.UI
{
    public static class UiSynchronization
    {
        private static SynchronizationContext _context;

        public static void Init(SynchronizationContext context = null)
        {
            _context = context ?? SynchronizationContext.Current;
        }

        public static SynchronizationContext SwitchToUiThread()
        {
            return _context;
        }
    }
}
