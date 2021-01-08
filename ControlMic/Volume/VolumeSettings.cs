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
        private const string SETTINGS = "audiosettings.json";
        private readonly CoreAudioController coreAudioController;
        private List<AudioSetting> settings = new();

        public VolumeSettings()
        {
            UiSynchronization.Init();
            InitializeComponent();
        }

        public VolumeSettings(CoreAudioController coreAudioController) : this()
        {
            this.coreAudioController = coreAudioController;
        }

        public async void Initialize()
        {
            if (File.Exists(SETTINGS))
                settings = JsonSerializer.Deserialize<List<AudioSetting>>(File.ReadAllText(SETTINGS));

            await UiSynchronization.SwitchToUiThread();

            ShowControls();
        }

        public void ShowControls()
        {
            flowLayoutPanel.Controls.AddRange(
                coreAudioController.DefaultPlaybackDevice.GetCapability<IAudioSessionController>()
                .Select(x =>
                {
                    var setting = settings.Find(y => x.Id == y.Id);

                    return new VolumeControl(x, setting?.Volume, setting?.IsLocked);
                }).ToArray());
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

            File.WriteAllText(SETTINGS, JsonSerializer.Serialize(settings));

            Hide();
        }
    }
}
