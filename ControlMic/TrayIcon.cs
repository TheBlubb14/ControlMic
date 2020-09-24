using System;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;

using AudioSwitcher.AudioApi;
using AudioSwitcher.AudioApi.CoreAudio;
using AudioSwitcher.AudioApi.Observables;

using GlobalHotKey;

namespace ControlMic
{
    public class TrayApplication : NativeWindow, IDisposable
    {
        private NotifyIcon notifyIcon;
        private ContextMenuStrip trayMenu;

        private CoreAudioDevice microphone;
        private readonly CoreAudioController coreAudioController;

        private readonly Icon mutedIcon;
        private readonly Icon unmutedIcon;

        private HotKey currentHotkey;
        private readonly HotKeyManager manager;

        private ToolStripMenuItem beepNotification;

        private bool beepEnabled;
        private BeepConfig onBeep;
        private BeepConfig offBeep;
        private Task pipeTask;

        private NotificationBallForm notificationBallForm;
        private IDisposable currentSubscription;

        public TrayApplication()
        {
            coreAudioController = new CoreAudioController();
            manager = new HotKeyManager();

            if (File.Exists("save.mm"))
            {
                using (var r = new BinaryReader(File.OpenRead("save.mm")))
                {
                    if (r.BaseStream.Length >= 40)
                    {
                        var g = new Guid(r.ReadString());
                        CoreAudioDevice mic = coreAudioController.GetDevice(g);

                        if (mic != null && mic.State == DeviceState.Active)
                            microphone = mic;
                        else
                            microphone = coreAudioController.GetDefaultDevice(DeviceType.Capture, Role.Communications);

                        beepEnabled = r.ReadBoolean();
                        Register((ModifierKeys)r.ReadInt32(), (Key)r.ReadInt32(), true);

                        if (r.BaseStream.Position != r.BaseStream.Length)
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

                        if (r.BaseStream.Position != r.BaseStream.Length)
                        {
                            notificationBallForm = new NotificationBallForm
                            {
                                Enabled = r.ReadBoolean()
                            };
                            notificationBallForm.DraggingEnd += (asd, esd) => Save();
                            notificationBallForm.Show();
                            notificationBallForm.Visible = notificationBallForm.Enabled;
                            notificationBallForm.Location = new Point(r.ReadInt32(), r.ReadInt32());

                        }
                        else
                        {
                            notificationBallForm = new NotificationBallForm
                            {
                                Enabled = false
                            };
                            notificationBallForm.Show();
                            notificationBallForm.Visible = false;
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

                notificationBallForm = new NotificationBallForm
                {
                    Enabled = false
                };
                notificationBallForm.Show();
                notificationBallForm.Visible = false;

                Register(ModifierKeys.None, Key.None);
            }

            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add(new ToolStripMenuItem("Toogle Mute", default, ToggleMute));
            trayMenu.Items.Add(new ToolStripMenuItem());
            trayMenu.Items.Add(new ToolStripMenuItem("Setup Shortcut", default, SetupShortcut));
            trayMenu.Items.Add(new ToolStripMenuItem("Notifications", default, NotificationMenuItems()));
            trayMenu.Items.Add(new ToolStripMenuItem("About", default, AboutDialog));
            trayMenu.Items.Add(new ToolStripMenuItem("Exit", default, Exit));

            var b = (Bitmap)System.Drawing.Image.FromFile(@"mic.png");
            IntPtr pIcon = b.GetHicon();
            var i = Icon.FromHandle(pIcon);
            mutedIcon = i;

            b = (Bitmap)System.Drawing.Image.FromFile(@"mic.png");
            for (var x = 0; x < b.Width; x++)
            {
                for (var y = 0; y < b.Height; y++)
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
                ContextMenuStrip = trayMenu,
                Visible = true
            };

            if (notificationBallForm.Enabled)
                notificationBallForm.Visible = !microphone.IsMuted;

            ChangeMicrophone(microphone);
            StartPipe();

        }

        public void StartPipe()
        {
            pipeTask = new Task(() =>
            {
                while (true)
                {
                    using (var server = new NamedPipeServerStream("ControlMicPipe", PipeDirection.In, 1))
                    {
                        server.WaitForConnection();
                        using (var reader = new StreamReader(server))
                        {
                            while (server.IsConnected)
                            {
                                if (reader.ReadLine() == "ToggleMic")
                                    ToggleMute();
                            }
                        }
                    }
                }
            }, TaskCreationOptions.LongRunning);
            pipeTask.Start();
        }

        private void AboutDialog(object sender, EventArgs e)
        => MessageBox.Show("Created by susch19 \r\nhttps://github.com/susch19/ControlMic \r\n\r\nIcon by Yannick Lung  \r\nhttps://www.iconfinder.com/icons/183597/microphone_record_icon", "About");

        private ToolStripMenuItem[] NotificationMenuItems()
        {
            var menuItems = new ToolStripMenuItem[3];
            beepNotification = new ToolStripMenuItem(beepEnabled ? "Beep ✓" : "Beep", default, (s, e) =>
                 {
                     beepEnabled = !beepEnabled;
                     beepNotification.Text = beepEnabled ? "Beep ✓" : "Beep";
                 });
            menuItems[0] = beepNotification;
            menuItems[1] = new ToolStripMenuItem("Configure beeps", default, ConfigureBeeps);
            menuItems[2] = new ToolStripMenuItem(notificationBallForm.Enabled ? "Notification Ball ✓" : "Notification Ball", default, (s, e) =>
            {
                if (notificationBallForm == null)
                {
                    notificationBallForm = new NotificationBallForm();
                    notificationBallForm.Show();
                    menuItems[2].Text = "Notification Ball ✓";
                    notificationBallForm.DraggingEnd += (asd, esd) => Save();
                    Save();
                }
                else
                {
                    notificationBallForm.Enabled = !notificationBallForm.Enabled;
                    notificationBallForm.SetVisibility(notificationBallForm.Enabled);
                    menuItems[2].Text = notificationBallForm.Enabled ? "Notification Ball ✓" : "Notification Ball";
                    Save();

                }
            });

            return menuItems;
        }


        private void ConfigureBeeps(object sender, EventArgs e) => MessageBox.Show("Not supperted at the moment");

        private void RefreshDevices()
        {
            trayMenu.Items.RemoveAt(1);

            trayMenu.Items.Insert(1, new ToolStripMenuItem("Devices", default,
                coreAudioController
                    .GetCaptureDevices()
                    .Where(x => x.State == DeviceState.Active)
                    .Select(
                    x => new ToolStripMenuItem((x == microphone ? "✓ " : "  ") + x.InterfaceName, default,
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
            currentSubscription = ObservableExtensions.Subscribe(x.MuteChanged, MuteChange);
        }

        private void MuteChange(DeviceMuteChangedArgs mic)
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

            if (notificationBallForm.Enabled)
                notificationBallForm.SetVisibility(!mic.IsMuted);


        }

        private void ThreadedBeep(int freq, int duration)
        {
            var t = new Task(() => Beep(freq, duration));
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
            var hotkeyControl = new System.Windows.Forms.TextBox()
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

            var save = new System.Windows.Forms.Button
            {
                Size = new Size(99, 20),
                Text = "SAVE",
                Top = 25,
                Left = 0
            };
            var close = new System.Windows.Forms.Button
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
            form.MaximumSize = new Size(440, 180);

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

        internal void ToggleMute(object sender = null, EventArgs e = null) => microphone?.ToggleMuteAsync();

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
                w.Write(notificationBallForm.Enabled);
                w.Write(notificationBallForm.Location.X);
                w.Write(notificationBallForm.Location.Y);
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
            coreAudioController.Dispose();
            //manager.Dispose();
            mutedIcon.Dispose();
            unmutedIcon.Dispose();

            GC.Collect();
            GC.SuppressFinalize(this);

        }

        [DllImport("kernel32.dll")]
        public static extern bool Beep(int freq, int duration);

    }

    internal struct BeepConfig
    {
        public int Freq { get; set; }
        public int Duration { get; set; }
    }
}
