using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;
using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using GlobalHotKey;
using System.Windows.Input;
using AudioSwitcher.AudioApi.Observables;
using System.IO;

namespace ControlMic
{
    public class TrayApplication : NativeWindow, IDisposable
    {
        private NotifyIcon notifyIcon;
        private ContextMenu trayMenu;

        private CoreAudioDevice microphone;
        private CoreAudioController coreAudioController;

        private Icon mutedIcon;
        private Icon unmutedIcon;

        private HotKey currentHotkey;
        private HotKeyManager manager;

        private MenuItem beepNotification;

        private bool beepEnabled;
        BeepConfig onBeep;
        BeepConfig offBeep;

        IDisposable currentSubscription;

        public TrayApplication()
        {
            coreAudioController = new CoreAudioController();
            manager = new HotKeyManager();

            if (File.Exists("save.mm"))
            {
                using (BinaryReader r = new BinaryReader(File.OpenRead("save.mm")))
                {
                    if (r.BaseStream.Length >= 40)
                    {
                        Guid g = new Guid(r.ReadString());
                        var mic = coreAudioController.GetCaptureDevices().Where(x=>x.State == DeviceState.Active).FirstOrDefault(x => x.Id == g);

                        if (mic != null)
                            microphone = mic;

                        beepEnabled = r.ReadBoolean();
                        Register((ModifierKeys)r.ReadInt32(), (Key)r.ReadInt32(), true);

                        if(r.BaseStream.Position != r.BaseStream.Length)
                        {
                            onBeep.Freq = r.ReadInt32();
                            onBeep.Duration = r.ReadInt32();
                            offBeep.Freq = r.ReadInt32();
                            offBeep.Duration = r.ReadInt32();
                        }
                        else
                        {
                            onBeep.Freq = 750;
                            onBeep.Duration = 200;
                            offBeep.Freq = 300;
                            offBeep.Duration = 200;
                        }
                    }
                }
            }
            else
            {
                microphone = coreAudioController.GetDefaultDevice(DeviceType.Capture, Role.Communications);
                beepEnabled = false;
                onBeep.Freq = 750;
                onBeep.Duration = 200;
                offBeep.Freq = 300;
                offBeep.Duration = 200;

                Register(ModifierKeys.None, Key.None);
            }

            trayMenu = new ContextMenu();
            trayMenu.MenuItems.Add(0, new MenuItem("Toogle Mute", ToggleMute));
            trayMenu.MenuItems.Add(1, new MenuItem());
            trayMenu.MenuItems.Add(2, new MenuItem("Setup Shortcut", SetupShortcut));
            trayMenu.MenuItems.Add(3, new MenuItem("Notifications", NotificationMenuItems()));
            trayMenu.MenuItems.Add(4, new MenuItem("About", AboutDialog));
            trayMenu.MenuItems.Add(5, new MenuItem("Exit", Exit));

            Bitmap b = (Bitmap)Bitmap.FromFile(@"mic.png");
            IntPtr pIcon = b.GetHicon();
            Icon i = Icon.FromHandle(pIcon);
            mutedIcon = i;

            b = (Bitmap)Bitmap.FromFile(@"mic.png");
            for (int x = 0; x < b.Width; x++)
            {
                for (int y = 0; y < b.Height; y++)
                {
                    if (b.GetPixel(x, y).A != 0)
                        b.SetPixel(x, y, Color.FromArgb(255, 255, 0, 0));
                }
            }

            pIcon = b.GetHicon();
            i = Icon.FromHandle(pIcon);
            unmutedIcon = i;
            i.Dispose();

            notifyIcon = new NotifyIcon()
            {
                Text = microphone.IsMuted ? "MuteMic - Muted" : "MuteMic - Unmuted",
                Icon = microphone.IsMuted ? mutedIcon : unmutedIcon,
                ContextMenu = trayMenu,
                Visible = true
            };

            ChangeMicrophone(microphone);
        }

        private void AboutDialog(object sender, EventArgs e)
        => MessageBox.Show("Created by susch19 \r\nhttps://github.com/susch19/ControlMic \r\n\r\nIcon by Yannick Lung  \r\nhttps://www.iconfinder.com/icons/183597/microphone_record_icon", "About");

        private MenuItem[] NotificationMenuItems()
        {
            var menuItems = new MenuItem[2];
            beepNotification = new MenuItem(beepEnabled ? "Beep ✓" : "Beep", (s,e) => 
                {
                    beepEnabled = !beepEnabled;
                    beepNotification.Text = beepEnabled ? "Beep ✓" : "Beep";
                });
            menuItems[0] = beepNotification;
            menuItems[1] = new MenuItem("Configure beeps", ConfigureBeeps);

            return menuItems;
        }

        private void ConfigureBeeps(object sender, EventArgs e)
        {
            MessageBox.Show("Not supperted at the moment");
        }

        private void RefreshDevices()
        {
            trayMenu.MenuItems.RemoveAt(1);

            trayMenu.MenuItems.Add(1, new MenuItem("Devices",
                coreAudioController
                    .GetCaptureDevices()
                    .Where(x => x.State == DeviceState.Active)
                    .Select(
                    x => new MenuItem((x == microphone ? "✓ " : "  ") + x.InterfaceName,
                        (o, s) =>
                        {
                            ChangeMicrophone(x);
                        })).ToArray()));
        }

        private void ChangeMicrophone(CoreAudioDevice x)
        {
            microphone = x;
            RefreshDevices();
            currentSubscription?.Dispose();
            currentSubscription = ObservableExtensions.Subscribe(x.MuteChanged, muteChange);
        }

        private void muteChange(DeviceMuteChangedArgs mic)
        {
            if (mic.Device != microphone)
                return;
            if (mic.IsMuted)
            {
                notifyIcon.Icon = mutedIcon;
                if (beepEnabled)
                    ThreadedBeep(offBeep.Freq, offBeep.Duration);
                notifyIcon.Text = "MuteMic - Muted";
            }
            else
            {
                notifyIcon.Icon = unmutedIcon;
                if (beepEnabled)
                    ThreadedBeep(onBeep.Freq, onBeep.Duration);
                notifyIcon.Text = "MuteMic - Unmuted";
            }
        }

        private void ThreadedBeep(int freq, int duration)
        {
            Task t = new Task(() => Beep(freq, duration));
            t.Start();
        }

        private void Notifications(object sender = null, EventArgs e = null)
        {
            beepEnabled = !beepEnabled;
            Save();
        }

        private void SetupShortcut(object sender = null, EventArgs e = null)
        {
            var form = new Form();
            var hotkeyControl = new TextBox()
            {
                Size = new Size(200, 20),
                Top = 0,
                Left = 0
            };
            hotkeyControl.KeyDown += (sen, even) =>
            {
                string temp;
                temp = (even.Modifiers & Keys.Control).ToString() == "None" ? "" : "Ctrl + ";
                temp += (even.Modifiers & Keys.Alt).ToString() == "None" ? "" : "Alt + ";
                temp += (even.Modifiers & Keys.Shift).ToString() == "None" ? "" : "Shift + ";
                temp += (even.KeyCode != Keys.ControlKey && even.KeyCode != Keys.Menu && even.KeyCode != Keys.ShiftKey) ? even.KeyCode.ToString() : "";
                hotkeyControl.Text = temp;
                even.Handled = true;
                even.SuppressKeyPress = true;
            };

            var save = new Button
            {
                Size = new Size(99, 20),
                Text = "SAVE",
                Top = 25,
                Left = 0
            };
            var close = new Button
            {
                Size = new Size(99, 20),
                Text = "CLOSE",
                Top = 25,
                Left = 101
            };

            form.Controls.Add(hotkeyControl);
            form.Controls.Add(save);
            form.Controls.Add(close);

            var size = new Size(220, 90);
            form.Size = size;
            form.MinimumSize = size;
            form.MaximumSize= new Size(440, 180);

            form.MinimizeBox = false;
            form.MaximizeBox = false;
            form.Text = "Hotkey - Setup";

            form.Show();
            save.MouseClick += (asd, esd) =>
            {
                var hotkeyText = hotkeyControl.Text.Replace(" ", "").Replace("Ctrl", "Control").Split('+');
                ModifierKeys modifiers = ModifierKeys.None;
                Key key = Key.None;

                foreach (var item in hotkeyText)
                {
                    if (Enum.TryParse(item, out ModifierKeys modifier))
                        modifiers |= modifier;

                    if (item != hotkeyText.Last())
                    {
                        continue;
                    }
                    else if (Enum.TryParse(item, out Key parseKey))
                    {
                        key = parseKey;
                    }
                    else
                    {
                        MessageBox.Show("Key " + hotkeyText.LastOrDefault() + " is not supported");
                        return;
                    }
                }
                Register(modifiers, key);
                form.Close();
            };

            close.MouseClick += (asd, esd) => form.Close();
        }

        private void Register(ModifierKeys modKeys, Key key, bool loading = false)
        {
            if (currentHotkey != null)
                manager.Unregister(currentHotkey);

            currentHotkey = new HotKey(key, modKeys);
            manager.Register(currentHotkey);
            manager.KeyPressed += Manager_KeyPressed;

            if (!loading)
                Save();
        }

        private void Manager_KeyPressed(object sender, KeyPressedEventArgs e) => ToggleMute();

        internal void ToggleMute(object sender = null, EventArgs e = null) => microphone.ToggleMute();

        private void Save()
        {
            using (var w = new BinaryWriter(File.OpenWrite("save.mm")))
            {
                w.Write(microphone.Id.ToString());
                w.Write(beepEnabled);
                w.Write((int)currentHotkey.Modifiers);
                w.Write((int)currentHotkey.Key);
                w.Write(onBeep.Freq);
                w.Write(onBeep.Duration);
                w.Write(offBeep.Freq);
                w.Write(offBeep.Duration);
            }
        }

        internal void Exit(object sender = null, EventArgs e = null)
        {
            Save();
            Dispose();
            Application.Exit();
        }

        public void Dispose()
        {
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            trayMenu.Dispose();
            notifyIcon = null;
            trayMenu = null;
            currentSubscription.Dispose();
            microphone.Dispose();
            coreAudioController.Dispose();
            manager.Dispose();
            mutedIcon.Dispose();
            unmutedIcon.Dispose();

            GC.Collect();
            GC.SuppressFinalize(this);

        }

        [DllImport("kernel32.dll")]
        public static extern bool Beep(int freq, int duration);
    }

    struct BeepConfig
    {
        public int Freq { get; set; }
        public int Duration { get; set; }
    }
}
