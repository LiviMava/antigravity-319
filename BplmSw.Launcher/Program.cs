using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BplmSw.Launcher
{
    class Program
    {
        // ================= Win32 API 定义 (用于窗口置顶) =================
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        private const int SW_RESTORE = 9;

        // ===============================================================

        // 定义共享文件路径 (需与插件端保持一致)
        private static string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BplmSwLauncher",
                "LoginArgs.json"
        );

        static void Main(string[] args)
        {
            try
            {
                // 1. 解析参数
                // 示例输入: BSWLauncher://eyJxx...,1813532,Z9_ComponentsRevision
                if (args.Length == 0) return;
                string rawUrl = args[0];
                // A. 去掉协议头 "BSWLauncher://" (忽略大小写)
                int schemeIndex = rawUrl.IndexOf("://");
                if (schemeIndex == -1) return;
                string dataPart = rawUrl.Substring(schemeIndex + 3);
                // B. URL 解码 (防止浏览器把逗号转义成 %2C)
                dataPart = Uri.UnescapeDataString(dataPart);
                // C. 去掉末尾可能的斜杠
                dataPart = dataPart.TrimEnd('/');
                // D. 按逗号分割
                string[] parts = dataPart.Split(',');
                if (parts.Length < 3)
                {
                    return;
                }
                string token = parts[0];
                string puid = parts[1];
                string type = parts[2];
                if ("undefined" == puid)
                    puid = null;
                if("undefined" == type)
                    type = null;
                // 3. 构造并写入 JSON 文件
                EnsureDirectoryExists();
                // 手动拼接 JSON 
                string jsonContent = $@"{{
                    ""token"": ""{token}"",
                    ""puid"": ""{puid}"",
                    ""type"": ""{type}"",
                    ""isWeb"": ""{1}""
                }}";
                File.WriteAllText(ConfigPath, jsonContent);

                // 3. 唤起或置顶 SolidWorks
                HandleSolidWorksProcess();
            }
            catch (Exception)
            {
            }
        }

        private static void EnsureDirectoryExists()
        {
            string dir = Path.GetDirectoryName(ConfigPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        private static void HandleSolidWorksProcess()
        {
            Process[] processes = Process.GetProcessesByName("SLDWORKS");

            if (processes.Length > 0)
            {
                // --- 情况 A: SW 已运行 (热启动) ---
                Process swProc = processes[0];
                IntPtr hwnd = swProc.MainWindowHandle;
                // 如果窗口被最小化了，还原
                if (IsIconic(hwnd))
                {
                    ShowWindowAsync(hwnd, SW_RESTORE);
                }
                // 强制置顶
                SetForegroundWindow(hwnd);
            }
            else
            {
                // --- 情况 B: SW 未运行 (冷启动) ---
                // 请修改为你实际的 SW 安装路径，或者读取注册表自动获取
                string swPath = @"D:\Program Files\SolidWorks Corp\SolidWorks\SLDWORKS.exe";
                if (File.Exists(swPath))
                {
                    Process.Start(swPath);
                }
            }
        }
    }
}