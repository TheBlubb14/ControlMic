using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;
using AudioSwitcher.AudioApi.Session;
using ControlMic.UI;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ControlMic.Volume
{
    public partial class VolumeSettings : Form
    {
        private const string PLAYBACK_SETTINGS = "audiosettings.json";
        private const string CAPTURE_SETTINGS = "capturesettings.json";
        private readonly CoreAudioController coreAudioController;
        private List<AudioSetting> settings = new();
        private readonly DeviceType deviceType;
        private readonly string settingsName;

        public VolumeSettings()
        {
            UiSynchronization.Init();
            InitializeComponent();
        }

        public VolumeSettings(CoreAudioController coreAudioController, DeviceType deviceType) : this()
        {
            this.deviceType = deviceType;
            this.coreAudioController = coreAudioController;

            settingsName = deviceType == DeviceType.Capture ? CAPTURE_SETTINGS : PLAYBACK_SETTINGS;

            if (File.Exists(settingsName))
                settings = JsonSerializer.Deserialize<List<AudioSetting>>(File.ReadAllText(settingsName));
        }

        public async void Initialize()
        {
            await UiSynchronization.SwitchToUiThread();

            ShowControls();
        }

        private void ShowControls()
        {
            if (deviceType == DeviceType.Capture)
            {
                flowLayoutPanel.Controls.AddRange(
                    coreAudioController.GetCaptureDevices(DeviceState.Active)
                    .Select(x =>
                    {
                        var setting = settings.Find(y => x.Id.ToString() == y.Id);

                        return new VolumeControl(x, setting?.Volume, setting?.IsLocked);
                    })
                    .ToArray());
            }
            else
            {
                flowLayoutPanel.Controls.AddRange(
                    coreAudioController.DefaultPlaybackDevice
                    .GetCapability<IAudioSessionController>()
                    .Select(x =>
                    {
                        var setting = settings.Find(y => x.Id == y.Id);

                        return new VolumeControl(x, setting?.Volume, setting?.IsLocked);
                    }).ToArray());
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
            else
            {
                // Dispose here
                base.OnFormClosing(e);
            }
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            foreach (var item in flowLayoutPanel.Controls.Cast<VolumeControl>())
            {
                // Remove old setting
                settings.RemoveAll(x => x.Id == item.Id);

                // Add updated one
                settings.Add(new(item.Id, item.Volume, item.Locked));
            }

            File.WriteAllText(settingsName, JsonSerializer.Serialize(settings));

            Hide();
        }
    }
}
