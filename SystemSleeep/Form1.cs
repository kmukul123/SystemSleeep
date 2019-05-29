using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SystemSleeep
{
    public partial class Form1 : Form
    {
        IdleTimer idletimer;
        volatile bool MonitorOff;
        public Form1()
        {
            InitializeComponent();
            checkBoxstartup.Checked= IsStartupSet();
            //System.Threading.Thread.Sleep(70000);
            MonitorOff = !SystemHelper.IsMonitorOn();
        }

        private void frmMain_Resize(object sender, EventArgs e)
        {
            if (FormWindowState.Minimized == this.WindowState)
            {
                notifyIcon1.BalloonTipText = "SystemSleep is running here";
                notifyIcon1.Visible = true;
                notifyIcon1.ShowBalloonTip(500);
                this.Hide();
            }

            else if (FormWindowState.Normal == this.WindowState)
            {
                //notifyIcon1.Visible = false;
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            //if (DateTime.Today.Year > 2016)
            //{
            //    MessageBox.Show("SystemSleeep: This application has expired");
            //    Application.Exit();
            //}

            string defaultidletime = GetSetting(this.DefaultIdleTimeConfigName);
            string sleepifMonitoSetting = GetSetting(this.sleepifMonitorOffConfigName);

            this.idletimer = new IdleTimer(int.Parse(defaultidletime));
            idletimer.IdleTimeReached += idletimer_IdleTimeReached;
            idletimer.BeforeIdleTimeReached += Idletimer_BeforeIdleTimeReached;
            idletimer.IdleTimeChanged += Idletimer_IdleTimeChanged;

            this.textBox1.Text = this.idletimer.GetInterval().ToString();
            this.sleepIfMonitorOff.Checked = bool.Parse(sleepifMonitoSetting);
        }

        private async void Idletimer_IdleTimeChanged(object sender, EventArgs e)
        {
            if (sleepIfMonitorOff.Checked)
            {
                MonitorOff = !SystemHelper.IsMonitorOn();
                if (MonitorOff)
                {
                    UpdateLabel2();
                    Idletimer_BeforeIdleTimeReached(sender, e);
                    await WaitAndSuspend();
                }
            }
            UpdateLabel2();

        }

        private void UpdateLabel2()
        {
            this.SetLavel2($"{DateTime.Now.ToString("hh:mm:ss")} Ïdle seconds:{idletimer.idletimeinsecs} MonitorOff:{MonitorOff}");
        }

        delegate void SetLabel2(string text);

        private void SetLavel2(string text)
        {
            // InvokeRequired required compares the thread ID of the
            // calling thread to the thread ID of the creating thread.
            // If these threads are different, it returns true.
            if (this.InvokeRequired)
            {
                SetLabel2 d = new SetLabel2(SetLavel2);
                this.Invoke(d, new object[] { text });
            }
            else
            {
                Trace.WriteLine(text);
                this.label2.Text = text;
            }
        }

        private void Idletimer_BeforeIdleTimeReached(object sender, EventArgs e)
        {
            notifyIcon1.BalloonTipText = "SystemSleep will put your system to sleep now";
            notifyIcon1.ShowBalloonTip(500);
        }

        async void idletimer_IdleTimeReached(object sender, EventArgs e)
        {
            //MonitorOff = !SystemHelper.IsMonitorOn();
            //MonitorOff = true;
            Trace.WriteLine("Idletimer reached MonitorOff:" + MonitorOff);
            
            //if (MonitorOff)
            {
                await WaitAndSuspend();
            }
            //else
            //{
            //    Trace.WriteLine("Monitor is on skipping sleep");
            //}
        }

        private async Task<bool> WaitAndSuspend()
        {
            try
            {
                //MonitorOff = !SystemHelper.IsMonitorOn();
                await Task.Delay(10 * 1000);
                var idletime = Win32Helper.GetIdleTimeInSecs();

                if (idletime > 30 )
                {
                    SystemHelper.Suspend();
                    notifyIcon1.BalloonTipText = $"SystemSleep had put your system to sleep idletime:{idletime} MonitorOff:{MonitorOff}";
                    notifyIcon1.ShowBalloonTip(500);
                    return true;
                }
                else
                {
                    notifyIcon1.BalloonTipText = $"Skipping sleep as is not off Monitoroff:{MonitorOff} idletime:{idletime}";
                }
            }
            catch (Exception ex)
            {
                notifyIcon1.BalloonTipText = $"Exception {ex.ToString()}";
                Trace.WriteLine(ex);
                notifyIcon1.ShowBalloonTip(500);
            }
            return false;
        }

        const int WM_SYSCOMMAND = 0x0112, SC_MONITORPOWER = 0xF170;
        
        [System.Security.Permissions.PermissionSet(System.Security.Permissions.SecurityAction.Demand, Name = "FullTrust")]
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_SYSCOMMAND) //Intercept System Command
            {
                //Trace.WriteLine("m.WParam.ToInt32() & 0xFFF0");
                if ((m.WParam.ToInt32() & 0xFFF0) == SC_MONITORPOWER)
                {
                    Trace.WriteLine("m.LParam=" + m.LParam);

                    if (m.LParam.ToInt32() != 2)
                    {
                        MonitorOff = false;
                    }
                    else
                    {
                        MonitorOff = true;
                        UpdateLabel2();

                    }
                    notifyIcon1.BalloonTipText = $"monitoroff={MonitorOff} lparam={m.LParam}";
                    notifyIcon1.ShowBalloonTip(500);
                    Trace.WriteLine($"monitoroff={MonitorOff} lparam={m.LParam}" );    
                }
            }

            base.WndProc(ref m);
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            this.ShowWindow();
        }

        private void notifyIcon1_MouseClick(object sender, MouseEventArgs e)
        {
            this.ShowWindow();
        }
        private void ShowWindow()
        {
            notifyIcon1.Visible = false;
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(this.textBox1.Text))
            {
                this.idletimer.SetInterval(Int16.Parse(this.textBox1.Text));
              UpdateSetting(this.DefaultIdleTimeConfigName, this.idletimer.GetInterval().ToString());
            }
        }


        private static void UpdateSetting(string key, string value)
        {
            Configuration configuration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            configuration.AppSettings.Settings[key].Value = value;
            configuration.Save();

            ConfigurationManager.RefreshSection("appSettings");
        }

        private static string GetSetting(String key)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            var ret = config.AppSettings.Settings[key];
            if (ret!=null) {
                return ret.Value;
            }
            else { return null; }
        }

        private void textBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = !char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar);
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void SleepNow_Click(object sender, EventArgs e)
        {
            SystemHelper.Suspend();
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            UpdateSetting(this.sleepifMonitorOffConfigName, this.sleepIfMonitorOff.Checked.ToString());

        }

        private void checkBoxstartup_CheckedChanged(object sender, EventArgs e)
        {
            RegistryKey rk = Registry.CurrentUser.OpenSubKey
            ("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

            if (checkBoxstartup.Checked)
                rk.SetValue("SystemSleeep", Application.ExecutablePath.ToString());
            else
                rk.DeleteValue("SystemSleeep", false);            
        }

        private bool IsStartupSet()
        {
            RegistryKey rk = Registry.CurrentUser.OpenSubKey
            ("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

            Object o = rk.GetValue("SystemSleeep");
            if (o != null)
                return true;
            else
                return false;
        }

    }
}
