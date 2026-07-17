using KartRider;
using RiderData;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;

namespace Profile
{
    public class ProfileService
    {
        private static readonly ConcurrentDictionary<string, object> ProfileLocks =
            new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        public static Setting SettingConfig { get; set; } = new Setting();

        public static ProfileConfig GetProfileConfig(string Nickname)
        {
            lock (GetProfileLock(Nickname))
            {
                if (!FileName.FileNames.ContainsKey(Nickname))
                {
                    FileName.Load(Nickname);
                }
                var filename = FileName.FileNames[Nickname];
                if (File.Exists(filename.config_path))
                {
                    return JsonHelper.DeserializeNoBom<ProfileConfig>(filename.config_path) ?? new ProfileConfig();
                }
                return new ProfileConfig();
            }
        }

        public static void SaveSettings()
        {
            var settingsDir = Path.GetDirectoryName(FileName.Load_Settings);
            if (!string.IsNullOrEmpty(settingsDir) && !Directory.Exists(settingsDir))
            {
                Directory.CreateDirectory(settingsDir);
            }
            File.WriteAllText(FileName.Load_Settings, JsonHelper.Serialize(SettingConfig));
        }

        public static void LoadSettings()
        {
            if (File.Exists(FileName.Load_Settings))
            {
                SettingConfig = JsonHelper.DeserializeNoBom<Setting>(FileName.Load_Settings) ?? new Setting();
            }
            else
            {
                SettingConfig = new Setting();
                SaveSettings();
            }
        }

        public static void Save(string Nickname, ProfileConfig config)
        {
            lock (GetProfileLock(Nickname))
            {
                if (!FileName.FileNames.ContainsKey(Nickname))
                {
                    FileName.Load(Nickname);
                }
                var filename = FileName.FileNames[Nickname];
                Directory.CreateDirectory(Path.GetDirectoryName(filename.config_path)!);
                string temporaryPath = filename.config_path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllText(temporaryPath, JsonHelper.Serialize(config), new UTF8Encoding(false));
                    File.Move(temporaryPath, filename.config_path, true);
                }
                finally
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
            }
        }

        public static void Load(string Nickname)
        {
            lock (GetProfileLock(Nickname))
            {
                if (!FileName.FileNames.ContainsKey(Nickname))
                {
                    FileName.Load(Nickname);
                }
                var filename = FileName.FileNames[Nickname];

                if (!File.Exists(filename.config_path))
                {
                    ProfileConfig newConfig = new ProfileConfig();
                    Save(Nickname, newConfig);
                }
                Loaded(Nickname);
            }
        }

        public static ProfileConfig Update(
            string nickname,
            Action<ProfileConfig> update)
        {
            if (update == null)
                throw new ArgumentNullException(nameof(update));

            lock (GetProfileLock(nickname))
            {
                ProfileConfig config = GetProfileConfig(nickname);
                update(config);
                Save(nickname, config);
                return config;
            }
        }

        private static object GetProfileLock(string nickname)
        {
            if (string.IsNullOrWhiteSpace(nickname))
                throw new ArgumentException("A profile nickname is required.", nameof(nickname));
            return ProfileLocks.GetOrAdd(nickname, _ => new object());
        }

        private static void Loaded(string Nickname)
        {
            var config = GetProfileConfig(Nickname);
            if (config.ServerSetting.PreventItem_Use == 0)
            {
                Program.PreventItem = false;
            }
            else
            {
                Program.PreventItem = true;
            }

            if (config.ServerSetting.SpeedPatch_Use == 0)
            {
                Program.SpeedPatch = false;
                if (Program.LauncherDlg != null)
                    Program.LauncherDlg.RefreshWindowTitle();
            }
            else
            {
                Program.SpeedPatch = true;
                if (Program.LauncherDlg != null)
                    Program.LauncherDlg.RefreshWindowTitle();
            }
        }
    }
}
