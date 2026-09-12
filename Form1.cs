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

        // ==========模拟系统静音按键API==========
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        private const byte VK_VOLUME_MUTE = 0xAD;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        /// <summary>切换系统主音量静音（模拟键盘静音键）</summary>
        private void ToggleSystemMute()
        {
            keybd_event(VK_VOLUME_MUTE, 0, 0, UIntPtr.Zero);
            keybd_event(VK_VOLUME_MUTE, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        /// <summary>SetSystemMute，这里调用切换静音【注意：这是切换模式】</summary>
        private void SetSystemMute(bool mute)
        {
            // ⚠重要：模拟按键只能切换状态！
            // 如果现在是未静音，调用ToggleSystemMute() → 静音
            // 如果现在是静音，调用ToggleSystemMute() → 取消静音
            ToggleSystemMute();
        }

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

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

                bool msgBoxOpen = IsMessageBoxOpened("提醒");
                if (msgBoxOpen)
                {
                    Console.WriteLine("检测到MessageBox弹窗打开");
                }

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
                    Console.WriteLine("检测到MessageBox弹窗打开");
                }

                if (_lockLeftSec <= 0)
                {
                    _isLockRunning = false;
                    timer2.Enabled = _isShutdownRunning;
                    btn_lock.Text = "静音锁屏";
                    SetSystemMute(true); // 执行静音
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