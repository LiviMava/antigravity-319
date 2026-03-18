using BplmSw.Common;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace BplmSw
{

    public static class TokenManager
    {
        private static readonly string _tokenPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BplmSwLauncher",
            "LoginArgs.json"
        );

        private static readonly string _credentialsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BplmSwLauncher",
            "credentials.json"
        );

        public static void SaveToken(string token)
        {
            try
            {
                string dir = Path.GetDirectoryName(_tokenPath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var json = new JObject();
                json["token"] = token;
                json["puid"] = "";
                json["type"] = "";
                json["isWeb"] = "0";

                // Use FileShare.ReadWrite to avoid locking issues with Launcher
                using (var fs = new FileStream(_tokenPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                using (var sw = new StreamWriter(fs))
                {
                    sw.Write(json.ToString());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving loginArgs: {ex.Message}");
            }
        }

        public static void LoadToken()
        {
            if (!File.Exists(_tokenPath)) return;

            try
            {
                using (var fs = new FileStream(_tokenPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs))
                {
                    var json = sr.ReadToEnd();
                    var jObj = JObject.Parse(json);

                    SessionContext.Token = jObj["token"]?.ToString();
                    SessionContext.Puid = jObj["puid"]?.ToString();
                    SessionContext.ObjectType = jObj["type"]?.ToString();
                    SessionContext.IsWeb = jObj["isWeb"]?.ToString();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading loginArgs: {ex.Message}");
                return;
            }
        }

        public static void ClearToken()
        {
            try
            {
                if (File.Exists(_tokenPath))
                {
                    File.Delete(_tokenPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error clearing loginArgs: {ex.Message}");
            }
        }

        public static void SaveCredentials(string username, string password, bool isRemember)
        {
            try
            {
                string dir = Path.GetDirectoryName(_credentialsPath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var json = new JObject();
                json["username"] = username;
                if (isRemember && !string.IsNullOrEmpty(password))
                {
                    try
                    {
                        // Encrypt password using DPAPI
                        byte[] data = Encoding.UTF8.GetBytes(password);
                        byte[] encrypted = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
                        json["password"] = Convert.ToBase64String(encrypted);
                        json["isRemember"] = true;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Encryption failed: {ex.Message}");
                        json["password"] = "";
                        json["isRemember"] = false;
                    }
                }
                else
                {
                    json["password"] = "";
                    json["isRemember"] = false;
                }

                File.WriteAllText(_credentialsPath, json.ToString());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving credentials: {ex.Message}");
            }
        }

        public static (string username, string password, bool isRemember) LoadCredentials()
        {
            if (!File.Exists(_credentialsPath)) return (null, null, false);

            try
            {
                var jsonStr = File.ReadAllText(_credentialsPath);
                var jObj = JObject.Parse(jsonStr);

                string username = jObj["username"]?.ToString();
                string encryptedPassword = jObj["password"]?.ToString();
                bool isRemember = jObj["isRemember"]?.ToObject<bool>() ?? false;
                string password = null;

                if (isRemember && !string.IsNullOrEmpty(encryptedPassword))
                {
                    try
                    {
                        // Decrypt password using DPAPI
                        byte[] encrypted = Convert.FromBase64String(encryptedPassword);
                        byte[] data = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                        password = Encoding.UTF8.GetString(data);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Decryption failed: {ex.Message}");
                        password = null;
                        isRemember = false;
                    }
                }

                return (username, password, isRemember);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading credentials: {ex.Message}");
                return (null, null, false);
            }
        }
    }
}
