using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TimerTool
{
    public partial class Form1 : Form
    {
        // 倒计时内存状态，避免依赖UI控件读取造成错乱
        private int _shutdownLeftSec = 0;
        private int _lockLeftSec = 0;
        private bool _isShutdownRunning = false;
        private bool _isLockRunning = false;

        // ==========Win32 API 用来检测MessageBox弹窗是否存在==========
        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        /// <summary>检测指定标题的MessageBox弹窗是否打开，MessageBox类名固定 #32770</summary>
        private bool IsMessageBoxOpened(string msgBoxTitle)
        {
            IntPtr hWnd = FindWindow("#32770", msgBoxTitle);
            return hWnd != IntPtr.Zero;
        }

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        /// <summary>设置系统静音/取消静音，CoreAudio底层API，无第三方依赖 Win10/11</summary>
        private void SetSystemMute(bool mute)
        {
            string cmd = mute
                ? @"Add-Type -TypeDefinition @'using System.Runtime.InteropServices;[Guid(""5CDF2C82-841E-4546-9722-0CF74078229A""), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IAudioEndpointVolume{int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, System.Guid pguidEventContext);}[Guid(""D666063F-1587-4E43-81F1-B948E807363F""), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IMMDevice{int Activate(ref System.Guid id, int clsCtx, IntPtr activationParams, out IAudioEndpointVolume endpoint);}[Guid(""A95664D2-9614-4F35-A746-DE8DB63617E6""), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IMMDeviceEnumerator{int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);}public static class Audio{public static void SetMute(bool mute){var enumeratorType = Type.GetTypeFromCLSID(new System.Guid(""BCDE0395-E52F-467C-8E3D-C4579291692E""));var enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(enumeratorType);enumerator.GetDefaultAudioEndpoint(0, 0, out IMMDevice dev);var g = typeof(IAudioEndpointVolume).GUID;dev.Activate(ref g, 1, IntPtr.Zero, out IAudioEndpointVolume vol);vol.SetMute(mute, System.Guid.Empty);}}'@;[Audio]::SetMute($true)"
                : @"Add-Type -TypeDefinition @'using System.Runtime.InteropServices;[Guid(""5CDF2C82-841E-4546-9722-0CF74078229A""), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IAudioEndpointVolume{int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, System.Guid pguidEventContext);}[Guid(""D666063F-1587-4E43-81F1-B948E807363F""), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IMMDevice{int Activate(ref System.Guid id, int clsCtx, IntPtr activationParams, out IAudioEndpointVolume endpoint);}[Guid(""A95664D2-9614-4F35-A746-DE8DB63617E6""), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IMMDeviceEnumerator{int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);}public static class Audio{public static void SetMute(bool mute){var enumeratorType = Type.GetTypeFromCLSID(new System.Guid(""BCDE0395-E52F-467C-8E3D-C4579291692E""));var enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(enumeratorType);enumerator.GetDefaultAudioEndpoint(0, 0, out IMMDevice dev);var g = typeof(IAudioEndpointVolume).GUID;dev.Activate(ref g, 1, IntPtr.Zero, out IAudioEndpointVolume vol);vol.SetMute(mute, System.Guid.Empty);}}'@;[Audio]::SetMute($false)";

            try
            {
                using (Process ps = new Process())
                {
                    ps.StartInfo.FileName = "powershell.exe";
                    ps.StartInfo.Arguments = $"-Command \"{cmd}\"";
                    ps.StartInfo.UseShellExecute = false;
                    ps.StartInfo.CreateNoWindow = true;
                    ps.Start();
                    ps.WaitForExit(1000);
                }
            }
            catch
            {
                //异常静默处理
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            timeTextBox.Text = DateTime.Now.ToString("yyyy‑MM‑dd HH:mm:ss");
        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            // 处理定时关机
            if (_isShutdownRunning)
            {
                _shutdownLeftSec--;

                //====这里判断是否运行MessageBox====
                // ⚠注意：原生MessageBox.Show()模态阻塞，弹窗弹出后timer直接停止执行，下面API只有弹窗关闭后才会执行到
                bool msgBoxOpen = IsMessageBoxOpened("提醒");
                if (msgBoxOpen)
                {
                    //MessageBox弹窗正在打开状态
                    Console.WriteLine("检测到MessageBox弹窗打开");
                }
                else
                {
                    //MessageBox已经关闭
                }
                //示例：剩余10秒弹出提醒【警告：原生MessageBox会卡住计时器！】
                //if (_shutdownLeftSec == 10)
                //{
                //    MessageBox.Show("即将关机！剩余10秒","提醒");
                //}

                if (_shutdownLeftSec <= 0)
                {
                    _isShutdownRunning = false;
                    timer2.Enabled = _isLockRunning;
                    Process.Start("shutdown", "/s /t 0");
                    return;
                }
                TimeSpan ts = TimeSpan.FromSeconds(_shutdownLeftSec);
                numericUpDown1.Value = ts.Hours;
                numericUpDown2.Value = ts.Minutes;
                numericUpDown3.Value = ts.Seconds;
            }

            // 处理定时静音锁屏
            if (_isLockRunning)
            {
                _lockLeftSec--;

                bool msgBoxOpen = IsMessageBoxOpened("提醒");
                if (msgBoxOpen)
                {
                    //MessageBox弹窗正在打开状态
                    Console.WriteLine("检测到MessageBox弹窗打开");
                }
                else
                {
                    //MessageBox已经关闭
                }
                //示例：剩余10秒弹出提醒【警告：原生MessageBox会卡住计时器！】
                //if (_lockLeftSec == 10)
                //{
                //    MessageBox.Show("即将静音锁屏！剩余10秒","提醒");
                //}

                if (_lockLeftSec <= 0)
                {
                    _isLockRunning = false;
                    timer2.Enabled = _isShutdownRunning;
                    btn_lock.Text = "静音锁屏";
                    SetSystemMute(true);
                    Process.Start("rundll32.exe", "user32.dll,LockWorkStation");
                    return;
                }
                TimeSpan ts = TimeSpan.FromSeconds(_lockLeftSec);
                numericUpDown1.Value = ts.Hours;
                numericUpDown2.Value = ts.Minutes;
                numericUpDown3.Value = ts.Seconds;
            }
        }

        private void shutdown_Click(object sender, EventArgs e)
        {
            Console.WriteLine("on shutdown_Click");
            if (shutdown.Text == "设置关机")
            {
                int hours = (int)numericUpDown1.Value;
                int minutes = (int)numericUpDown2.Value;
                int seconds = (int)numericUpDown3.Value;
                int total = hours * 3600 + minutes * 60 + seconds;

                if (total == 0)
                {
                    IsShutdownNow isShutdownNow = new IsShutdownNow();
                    isShutdownNow.ShowDialog();
                    return;
                }

                _shutdownLeftSec = total;
                _isShutdownRunning = true;
                timer2.Enabled = true;
                shutdown.Text = "取消关机";
                MessageBox.Show($"电脑将于 {total}s 后关机", "提醒");
            }
            else
            {
                _isShutdownRunning = false;
                _shutdownLeftSec = 0;
                shutdown.Text = "设置关机";
                timer2.Enabled = _isLockRunning;

                numericUpDown1.Enabled = true;
                numericUpDown2.Enabled = true;
                numericUpDown3.Enabled = true;
            }
        }

        private void btn_lock_Click(object sender, EventArgs e)
        {
            Console.WriteLine("on btn_lock_Click " + btn_lock.Text);
            if (btn_lock.Text == "静音锁屏")
            {
                int hours = (int)numericUpDown1.Value;
                int minutes = (int)numericUpDown2.Value;
                int seconds = (int)numericUpDown3.Value;
                int total = hours * 3600 + minutes * 60 + seconds;

                if (total <= 0)
                {
                    // 立即执行：静音+锁屏
                    SetSystemMute(true);
                    Process.Start("rundll32.exe", "user32.dll,LockWorkStation");
                    return;
                }

                _lockLeftSec = total;
                _isLockRunning = true;
                timer2.Enabled = true;
                btn_lock.Text = "取消锁屏";
                MessageBox.Show($"电脑将于 {total}s 后静音锁屏", "提醒");
            }
            else
            {
                _isLockRunning = false;
                _lockLeftSec = 0;
                btn_lock.Text = "静音锁屏";
                timer2.Enabled = _isShutdownRunning;

                numericUpDown1.Enabled = true;
                numericUpDown2.Enabled = true;
                numericUpDown3.Enabled = true;
            }
        }

        private void shutdown_cancel_Click(object sender, EventArgs e)
        {
            _isShutdownRunning = false;
            _shutdownLeftSec = 0;
            shutdown.Text = "设置关机";
            timer2.Enabled = _isLockRunning;

            numericUpDown1.Enabled = true;
            numericUpDown2.Enabled = true;
            numericUpDown3.Enabled = true;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {

        }

        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        //====下面是自动生成的空事件，保留不删除，防止设计器报错====
        private void label1_Click(object sender, EventArgs e) { }
        private void label5_Click(object sender, EventArgs e) { }
        private void label7_Click(object sender, EventArgs e) { }
        private void label9_Click(object sender, EventArgs e) { }
        private void textBox2_TextChanged(object sender, EventArgs e) { }
        private void textBox5_TextChanged(object sender, EventArgs e) { }
        private void textBox6_TextChanged(object sender, EventArgs e) { }
        private void wifitext_TextChanged(object sender, EventArgs e) { }
        private void textBox1_TextChanged(object sender, EventArgs e) { }
        private void numericUpDown1_ValueChanged(object sender, EventArgs e) { }
        private void numericUpDown2_ValueChanged(object sender, EventArgs e) { }
        private void numericUpDown3_ValueChanged(object sender, EventArgs e) { }
        private void textBox2_TextChanged_1(object sender, EventArgs e) { }
    }
}