using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.Observables;
using AudioSwitcher.AudioApi.Session;
using ControlMic.UI;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ControlMic.Volume
{
    public partial class VolumeControl : UserControl
    {
        public double Volume => volume;

        public async Task SetVolume(double value)
        {
            if (volume == value)
                return;

            suppressChangedEvent = true;
            // We need to round for the acpturing devices
            // some return 99.9999008178711
            // On the other hand we have to consider up to 3 digits
            // for playback devices like VLC
            trackBarVolume.Value = (int)Math.Round(value, 3);
            numericUpDownVolume.Value = (int)Math.Round(value, 3);
            volume = value;

            if ((device is null ? session.Volume : device.Volume) != Volume)
                await SetVolumeWrapper(Volume);

            suppressChangedEvent = false;
        }

        public bool Locked { get; private set; }

        public string Id => device is null ? session.Id : device.Id.ToString();

        private readonly IAudioSession session;
        private readonly IDevice device;
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

        public VolumeControl(IDevice device, double? volume = null, bool? locked = null) : this()
        {
            this.device = device;
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
                v = volume ?? await GetVolumeWrapper();
            }
            else
            {
                v = await GetVolumeWrapper();
            }

            volumeChanged = device is null ?
                session.VolumeChanged.Subscribe(x => VolumeChanged(x.Volume)) :
                device.VolumeChanged.Subscribe(x => VolumeChanged(x.Volume));

            await UiSynchronization.SwitchToUiThread();
            SetVolume(v);
        }

        private async Task<double> GetVolumeWrapper()
        {
            return device is null ?
                await session.GetVolumeAsync() :
                await device.GetVolumeAsync();
        }

        private async Task<double> SetVolumeWrapper(double volume)
        {
            return device is null ?
                await session.SetVolumeAsync(volume) :
                await device.SetVolumeAsync(volume);
        }

        private async void VolumeChanged(double volume)
        {
            await UiSynchronization.SwitchToUiThread();

            // Has changed?
            if (volume != Volume)
            {
                if (Locked)
                {
                    // Revert
                    await SetVolumeWrapper(Volume);
                }
                else
                {
                    SetVolume(volume);
                }
            }
        }

        private void trackBarVolume_ValueChanged(object sender, EventArgs e)
        {
            if (suppressChangedEvent)
                return;

            SetVolume(trackBarVolume.Value);
        }

        private void ShowIcon()
        {
            if (device is null)
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
            else
            {
                labelName.Text = device.FullName;
                var icons = device.IconPath.Split(',');
                pictureBox.Image = ExtractIcon(icons[0], int.Parse(icons[1]), true)?.ToBitmap();
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

            SetVolume((double)numericUpDownVolume.Value);
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
