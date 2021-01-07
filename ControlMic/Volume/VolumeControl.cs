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
        public double Volume
        {
            get => volume;
            set
            {
                if (volume == value)
                    return;

                suppressChangedEvent = true;
                trackBarVolume.Value = (int)value;
                numericUpDownVolume.Value = (int)value;
                volume = value;

                if (session.Volume != Volume)
                    new Action(async () => await session.SetVolumeAsync(Volume)).Invoke();

                suppressChangedEvent = false;
            }
        }

        public bool Locked { get; private set; }

        public string Id => session.Id;

        private readonly IAudioSession session;
        private IDisposable volumeChanged;
        private bool suppressChangedEvent;
        private double volume;

        public VolumeControl()
        {
            InitializeComponent();
        }

        public VolumeControl(IAudioSession session, double? volume = null, bool? locked = null) : this()
        {
            this.session = session;
            Initialize(volume, locked);
        }

        private async void Initialize(double? volume = null, bool? locked = null)
        {
            ShowIcon();

            double v;

            if (locked == true)
            {
                Locked = true;
                checkBoxLocked.Checked = true;

                // Set volume to provided value or the current
                v = volume ?? await session.GetVolumeAsync();
            }
            else
            {
                v = await session.GetVolumeAsync();
            }

            volumeChanged = session.VolumeChanged.Subscribe(VolumeChanged);

            await UiSynchronization.SwitchToUiThread();
            Volume = v;
        }

        private async void VolumeChanged(SessionVolumeChangedArgs e)
        {
            await UiSynchronization.SwitchToUiThread();

            // Has changed?
            if (e.Volume != Volume)
            {
                if (Locked)
                {
                    // Revert
                    await session.SetVolumeAsync(Volume);
                }
                else
                {
                    Volume = e.Volume;
                }
            }
        }

        private void trackBarVolume_ValueChanged(object sender, EventArgs e)
        {
            if (suppressChangedEvent)
                return;

            Volume = trackBarVolume.Value;
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

        private void checkBoxLocked_CheckedChanged(object sender, EventArgs e)
        {
            Locked = checkBoxLocked.Checked;
        }

        private void numericUpDownVolume_ValueChanged(object sender, EventArgs e)
        {
            if (suppressChangedEvent)
                return;

            Volume = (double)numericUpDownVolume.Value;
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
