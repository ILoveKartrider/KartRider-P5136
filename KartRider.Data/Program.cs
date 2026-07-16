using KartLibrary.Consts;
using KartLibrary.Data;
using KartLibrary.File;
using KartLibrary.Xml;
using KartRider.IO.Packet;
using Microsoft.Win32;
using Profile;
using LoggerLibrary;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Linq;
using System.Xml.Linq;
using System.Globalization;
using KartRider.Common.Data;
using KartRider.Common.Security;
using KartRider.Compatibility;

namespace KartRider
{
    internal static class Program
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetConsoleWindow();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        public const int SW_HIDE = 0;
        public const int SW_SHOW = 5;
        private const int STD_INPUT_HANDLE = -10;
        private const uint ENABLE_QUICK_EDIT_MODE = 0x0040;
        private const uint ENABLE_EXTENDED_FLAGS = 0x0080;
        public static bool isVisible = true;
        public static IntPtr consoleHandle;

        public static Launcher LauncherDlg;
        public static Setting SettingDlg;
        public static bool SpeedPatch;
        public static bool PreventItem;
        public static Encoding targetEncoding = Encoding.UTF8;

        [STAThread]
        private static void Main(string[] args)
        {
            // 分配控制台
            AllocConsole();
            consoleHandle = GetConsoleWindow();
            DisableConsoleQuickEdit();

            // 保存原始输出流
            var originalOut = Console.Out;

            // 创建缓存编写器并替换控制台输出
            CachedConsoleWriter.cachedWriter = new CachedConsoleWriter(originalOut);
            Console.SetOut(CachedConsoleWriter.cachedWriter);

            // 初始化自适应编码
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            SetAdaptiveConsoleEncoding();

            if (args != null && args.Length > 0)
            {
                Console.WriteLine("서버 런처는 파일 패킹 인자를 처리하지 않습니다. 전달된 인자를 무시합니다.");
            }

            ProfileService.LoadSettings();

            // The server package is self-describing: its adjacent game data fixes
            // the protocol/build it serves. Never borrow a client launcher's active
            // HKCU gamepath, because that can silently switch the server to another
            // client instance.
            string rootDirectory = Path.GetFullPath(FileName.appDir);
            ClientBuildProfile localProfile = ClientBuildDetector.Detect(rootDirectory);
            if (!File.Exists(FileName.KartRider) ||
                (!localProfile.IsLegacy && !File.Exists(FileName.pinFile)))
            {
                MessageBox.Show(
                    "서버 대상 KartRider.exe를 찾을 수 없습니다.\n" +
                    "서버 런처를 지원되는 게임 데이터 폴더에 두세요.",
                    "서버 데이터 없음",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            ClientBuildProfile clientProfile = ClientBuildDetector.DetectAndActivate(rootDirectory);
            Console.WriteLine($"서버 대상 빌드: {clientProfile.DisplayName}");
            string pinFile = Path.GetFullPath(Path.Combine(rootDirectory, @"KartRider.pin"));

            if (clientProfile.UsesModernPin)
            {
                PINFile val = new PINFile(pinFile);
                ProfileService.SettingConfig.ClientVersion = val.Header.MinorVersion;
                ProfileService.SettingConfig.LocaleID = val.Header.LocaleID;
                ProfileService.SettingConfig.nClientLoc = val.Header.Unk2;
            }
            else
            {
                ProfileService.SettingConfig.ClientVersion = clientProfile.ProtocolVersion;
                ProfileService.SettingConfig.LocaleID = clientProfile.LocaleId;
                ProfileService.SettingConfig.nClientLoc = clientProfile.ClientLocation;
            }
            ProfileService.SaveSettings();

            Load_Data();

            try
            {
                // This executable is the server launcher. IPv6 client-forwarding
                // mode belonged to the old combined launcher and must never run
                // here; unsupported listen addresses are reported by the host.
                ClientServerRuntime.Start(rootDirectory);
            }
            catch (System.Net.Sockets.SocketException)
            {
                LauncherSystem.MessageBoxType2();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"서버 시작 실패: {ex.Message}");
            }

            if (!ProfileService.SettingConfig.Console)
            {
                ShowWindow(consoleHandle, SW_HIDE);
                isVisible = false;
            }
            if (ProfileService.SettingConfig.EnableMod)
            {
                ModManager.Initialize(rootDirectory);
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Launcher startLauncher = new Launcher();
            Program.LauncherDlg = startLauncher;
            Program.LauncherDlg.kartRiderDirectory = rootDirectory;
            Application.Run(startLauncher);
        }

        private static void DisableConsoleQuickEdit()
        {
            IntPtr inputHandle = GetStdHandle(STD_INPUT_HANDLE);
            if (inputHandle == IntPtr.Zero || inputHandle == new IntPtr(-1) ||
                !GetConsoleMode(inputHandle, out uint mode))
            {
                return;
            }

            // QuickEdit/Mark mode suspends synchronous console writers while text
            // is selected. Packet handling must never depend on that UI state.
            mode |= ENABLE_EXTENDED_FLAGS;
            mode &= ~ENABLE_QUICK_EDIT_MODE;
            SetConsoleMode(inputHandle, mode);
        }

        public static void SetAdaptiveConsoleEncoding()
        {
            try
            {
                // 1. 检测操作系统类型
                bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

                // 2. 优先尝试 UTF-8（跨平台通用）
                targetEncoding = Encoding.UTF8;

                // 3. Windows 中文环境特殊处理（部分终端默认 GBK）
                if (isWindows)
                {
                    try
                    {
                        // 注册表路径
                        string codePageRegPath = "HKEY_LOCAL_MACHINE\\SYSTEM\\CurrentControlSet\\Control\\Nls\\CodePage";

                        // 读取 OEMCP 值（返回 object 类型，需判断是否为 null）
                        object oemcpObj = Registry.GetValue(codePageRegPath, "OEMCP", null);

                        // 正确判断：是否读取到有效值，且能转换为 int
                        if (oemcpObj != null && int.TryParse(oemcpObj.ToString(), out int oemcp))
                        {
                            try
                            {
                                // 获取对应编码
                                targetEncoding = Encoding.GetEncoding(oemcp);
                            }
                            catch (ArgumentException)
                            {
                                // 编码不支持时回退到 UTF-8
                                targetEncoding = Encoding.UTF8;
                            }
                        }
                        else
                        {
                            // 未读取到 OEMCP 值
                            targetEncoding = Encoding.UTF8;
                        }
                    }
                    catch (Exception ex)
                    {
                        // 捕获注册表读取异常（如权限不足）
                        targetEncoding = Encoding.UTF8;
                    }
                }
                // 4. 应用编码设置（输出/输入保持一致）
                Console.OutputEncoding = targetEncoding;
                Console.InputEncoding = targetEncoding;

                // 5. 验证编码是否生效（可选）
                Console.WriteLine($"적용된 인코딩: {targetEncoding.EncodingName}");
            }
            catch (Exception ex)
            {
                // 异常时使用系统默认编码作为最后保障
                Console.WriteLine($"인코딩 설정 실패, 기본 인코딩 사용: {ex.Message}");
            }
        }

        public static void Load_Data()
        {
            try
            {
                string localFilePath = FileName.ModelMax_LoadFile;
                
                // 确保目录存在
                string directory = Path.GetDirectoryName(localFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 如果本地文件不存在，直接创建
                if (!File.Exists(localFilePath))
                {
                    File.WriteAllText(localFilePath, ModelMax.XmlContent);
                    Console.WriteLine($"ModelMax.xml 생성 완료: {localFilePath}");
                }
                else
                {
                    // 加载本地和资源XML
                    XDocument localXml = XDocument.Load(localFilePath);
                    XDocument resourceXml = XDocument.Parse(ModelMax.XmlContent);
                    
                    int addedCount = 0;
                    
                    // 遍历资源中的所有 kart 节点
                    foreach (var resourceKart in resourceXml.Root!.Elements("kart"))
                    {
                        string? id = resourceKart.Attribute("id")?.Value;
                        string? name = resourceKart.Attribute("name")?.Value;
                        
                        if (id != null)
                        {
                            // 检查本地是否已存在该ID
                            bool exists = localXml.Root!.Elements("kart")
                                .Any(k => k.Attribute("id")?.Value == id);
                            
                            if (!exists)
                            {
                                // 复制 kart 元素（包含所有属性）
                                localXml.Root.Add(new XElement(resourceKart));
                                Console.WriteLine($"카트 추가: {name ?? id}");
                                addedCount++;
                            }
                        }
                    }
                    
                    if (addedCount > 0)
                    {
                        localXml.Save(localFilePath, SaveOptions.None);
                        Console.WriteLine($"ModelMax.xml 업데이트 완료, 카트 {addedCount}개 추가");
                    }
                    else
                    {
                        Console.WriteLine("ModelMax.xml이 최신 상태입니다.");
                    }
                }

                SpecialKartConfig.SaveConfigToFile(FileName.SpecialKartConfig);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"데이터 불러오기 실패: {ex.Message}");
            }
        }
    }
}
