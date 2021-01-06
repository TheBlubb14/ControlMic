using AudioSwitcher.AudioApi.Observables;
using AudioSwitcher.AudioApi.Session;
using ControlMic.UI;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ControlMic.Volume
{
    public partial class VolumeControl : UserControl
    {
        public int Volume => trackBarVolume.Value;

        public bool Locked { get; private set; }

        public string Id => session.Id;

        private readonly IAudioSession session;
        private IDisposable volumeChanged;

        public VolumeControl()
        {
            InitializeComponent();
        }

        public VolumeControl(IAudioSession session, int? volume = null, bool? locked = null) : this()
        {
            this.session = session;
            Initialize(volume, locked);
        }

        private async void Initialize(int? volume = null, bool? locked = null)
        {
            ShowIcon();

            int v;

            if (locked == true)
            {
                Locked = true;
                UpdateLabel();

                // Set volume to provided value or the current
                v = volume ?? (int)await session.GetVolumeAsync();
            }
            else
            {
                v = (int)await session.GetVolumeAsync();
            }

            volumeChanged = session.VolumeChanged.Subscribe(VolumeChanged);

            await UiSynchronization.SwitchToUiThread();

            trackBarVolume.Value = v;
            UpdateLabel();
        }

        private async void VolumeChanged(SessionVolumeChangedArgs e)
        {
            await UiSynchronization.SwitchToUiThread();

            var v = (int)e.Volume;

            // Has changed?
            if (v != Volume)
            {
                if (Locked)
                {
                    // Revert
                    await session.SetVolumeAsync(Volume);
                }
                else
                {
                    // Update UI
                    trackBarVolume.Value = v;
                }
            }

            UpdateLabel();
        }

        private async void trackBarVolume_ValueChanged(object sender, EventArgs e)
        {
            if ((int)session.Volume != Volume)
                await session.SetVolumeAsync(Volume);
        }

        private void ShowIcon()
        {
            var process = Process.GetProcessById(session.ProcessId);

            if (session.IsSystemSession)
            {
                labelName.Text = "Windows Sounds";
                var icons = session.IconPath.Split(',');
                pictureBox.Image = ExtractIcon(icons[0], int.Parse(icons[1]), true)?.ToBitmap();
            }
            else
            {
                labelName.Text = string.IsNullOrWhiteSpace(session.DisplayName) ? process.MainWindowTitle : session.DisplayName;
                try
                {
                    pictureBox.Image = Icon.ExtractAssociatedIcon(process.MainModule.FileName).ToBitmap();
                }
                catch (Win32Exception)
                {
                    // eg could not read TeamSpeak MainModule
                }
            }
        }

        private void labelVolume_Click(object sender, System.EventArgs e)
        {
            Locked = !Locked;
            UpdateLabel();
        }

        private void UpdateLabel()
        {
            labelVolume.Text = $"{Volume}{(Locked ? " 🔒" : "")}";
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            volumeChanged?.Dispose();
            base.OnHandleDestroyed(e);
        }

        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int ExtractIconEx(string sFile, int iIndex, out IntPtr piLargeVersion, out IntPtr piSmallVersion, int amountIcons);

        private static Icon ExtractIcon(string file, int number, bool largeIcon)
        {
            _ = ExtractIconEx(Environment.ExpandEnvironmentVariables(file).Replace("@", ""), number, out IntPtr large, out IntPtr small, 1);
            try
            {
                return Icon.FromHandle(largeIcon ? large : small);
            }
            catch
            {
                return null;
            }
        }
    }
}
