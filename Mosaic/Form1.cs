using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Timers;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using System.Windows.Forms;
using System;
using Microsoft.VisualBasic.Devices;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Runtime.CompilerServices;
using Quobject.SocketIoClientDotNet.Client;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WebSocketSharp;

namespace Mosaic
{
    public partial class Form1 : Form
    {
        static int MosaicSize = 60;
        public string filePath { get; set; }

        int countDuty = 1;
        public Form1()
        {
            InitializeComponent();
        }

        WebSocket _WS = new WebSocket("ws://localhost:5001/");

        KeyboardHook KeyboardHook = new KeyboardHook();

        private void Form1_Load(object sender, EventArgs e)
        {
            pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;
            KeyboardHook.KeyDownEvent += KeyDown;
            KeyboardHook.Hook();
            _WS.Connect();
            _WS.Send("message");
            _WS.OnMessage += (sender, e) =>
            {
                if (e.Data == "stop")
                {
                    timer.Stop();
                }

                if (e.Data == "start")
                {
                    if (!timer.Enabled)
                    {
                        timer = new System.Timers.Timer(300);
                        timer.Elapsed += Mosaic;
                        counter = 1;
                        countDuty = 1;
                        timer.Start();
                    }
                    else
                    {
                        timer.Start();
                    }
                }
                if(e.Data == "speedUp")
                {
                    countDuty = 5;
                }
            };
        }
        int counter = 1;
        System.Timers.Timer timer = new System.Timers.Timer(300);

        private void KeyDown(object sender, KeyEventArg e)
        {

        }

        private Mat Process(Mat img, int size)
        {
            //Mat img = img_;
            int w = img.Width;
            int h = img.Height;
            Cv2.Resize(img, img, new OpenCvSharp.Size(w / size, h / size));
            Cv2.Resize(img, img, new OpenCvSharp.Size(w, h));
            return img;
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
        }

        private void Mosaic(Object source, ElapsedEventArgs e)
        {
            if (counter < MosaicSize - 1)
            {
                using (Mat originImage = Process(new Mat(filePath), MosaicSize - counter))
                {
                    if (pictureBox1.Image != null)
                    {
                        pictureBox1.Image.Dispose();
                    }
                    pictureBox1.Image = BitmapConverter.ToBitmap(originImage);
                    originImage.Dispose();
                    counter+=countDuty;
                }
            }
            else
            {
                timer.Stop();
                using (Mat originImage = Process(new Mat(filePath), 1))
                {
                    if (pictureBox1.Image != null)
                    {
                        pictureBox1.Image.Dispose();
                    }
                    pictureBox1.Image = BitmapConverter.ToBitmap(originImage);
                    originImage.Dispose();
                }
            }
        }

        private void panel1_Paint(object sender, PaintEventArgs e)
        {

        }

        private void Drop(object sender, DragEventArgs e)
        {
            var _filePath = (string[])e.Data.GetData(DataFormats.FileDrop, false);
            filePath = string.Join("", _filePath);
        }

        private void pnlDragAndDrop_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
        }
    }

    class KeyboardHook
    {
        protected const int WH_KEYBOARD_LL = 0x000D;
        protected const int WM_KEYDOWN = 0x0100;
        protected const int WM_KEYUP = 0x0101;
        protected const int WM_SYSKEYDOWN = 0x0104;
        protected const int WM_SYSKEYUP = 0x0105;

        [StructLayout(LayoutKind.Sequential)]
        public class KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public KBDLLHOOKSTRUCTFlags flags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [Flags]
        public enum KBDLLHOOKSTRUCTFlags : uint
        {
            KEYEVENTF_EXTENDEDKEY = 0x0001,
            KEYEVENTF_KEYUP = 0x0002,
            KEYEVENTF_SCANCODE = 0x0008,
            KEYEVENTF_UNICODE = 0x0004,
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, KeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private delegate IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private KeyboardProc proc;
        private IntPtr hookId = IntPtr.Zero;

        public void Hook()
        {
            if (hookId == IntPtr.Zero)
            {
                proc = HookProcedure;
                using (var curProcess = Process.GetCurrentProcess())
                {
                    using (ProcessModule curModule = curProcess.MainModule)
                    {
                        hookId = SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
                    }
                }
            }
        }

        public void UnHook()
        {
            UnhookWindowsHookEx(hookId);
            hookId = IntPtr.Zero;
        }

        public IntPtr HookProcedure(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                var kb = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                var vkCode = (int)kb.vkCode;
                OnKeyDownEvent(vkCode);
            }
            else if (nCode >= 0 && (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP))
            {
                var kb = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                var vkCode = (int)kb.vkCode;
                OnKeyUpEvent(vkCode);
            }
            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        public delegate void KeyEventHandler(object sender, KeyEventArg e);
        public event KeyEventHandler KeyDownEvent;
        public event KeyEventHandler KeyUpEvent;

        protected void OnKeyDownEvent(int keyCode)
        {
            KeyDownEvent?.Invoke(this, new KeyEventArg(keyCode));
        }
        protected void OnKeyUpEvent(int keyCode)
        {
            KeyUpEvent?.Invoke(this, new KeyEventArg(keyCode));
        }

    }

    public class KeyEventArg : EventArgs
    {
        public int KeyCode { get; }

        public KeyEventArg(int keyCode)
        {
            KeyCode = keyCode;
        }
    }
}