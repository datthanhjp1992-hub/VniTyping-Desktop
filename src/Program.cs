// VniTyping — bộ gõ tiếng Việt portable cho Windows.
// Copyright (C) 2026 Nguyễn Thành Đạt. GPL v2 hoặc mới hơn (xem COPYING).
using System;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using VniTyping.UI;

[assembly: AssemblyTitle("VniTyping")]
[assembly: AssemblyDescription("Bộ gõ tiếng Việt portable cho Windows")]
[assembly: AssemblyProduct("VniTyping")]
[assembly: AssemblyCopyright("Copyright © 2026 Nguyễn Thành Đạt · GPL v2+")]
[assembly: AssemblyVersion("1.0.1.0")]
[assembly: AssemblyFileVersion("1.0.1.0")]
[assembly: AssemblyInformationalVersion("1.0.1")]

namespace VniTyping
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool first;
            using (var mutex = new Mutex(true, "VniTyping.SingleInstance.7d3f", out first))
            {
                if (!first)
                {
                    // đã có một bản đang chạy: gọi cửa sổ đó lên rồi thoát
                    try
                    {
                        NativeMethods.PostMessage(NativeMethods.HWND_BROADCAST, MainForm.ShowMessage, IntPtr.Zero, IntPtr.Zero);
                    }
                    catch (Exception)
                    {
                        // bỏ qua
                    }
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm(Settings.Load()));
                GC.KeepAlive(mutex);
            }
        }
    }
}
