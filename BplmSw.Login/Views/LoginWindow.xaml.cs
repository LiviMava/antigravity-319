using BplmSw.Common;
using System;
using System.IO;
using System.Reflection;
using System.Windows;

namespace BplmSw.Login
{
    public partial class LoginWindow : Window
    {

        public static string GetParentDirectoryOfExecutingAssembly()
        {
            // 获取当前执行程序集的位置
            string assemblyLocation = Assembly.GetExecutingAssembly().Location;

            // 转换为文件信息
            FileInfo assemblyFile = new FileInfo(assemblyLocation);

            // 获取目录信息
            DirectoryInfo assemblyDir = assemblyFile.Directory;

            if (assemblyDir?.Parent?.Parent != null)
            {
                return assemblyDir.Parent.Parent.FullName;
            }

            throw new DirectoryNotFoundException("无法找到上上级目录");
        }
        public static string SwHomePath = GetParentDirectoryOfExecutingAssembly();

        public LoginWindow()
        {
            InitializeComponent();
            ServerConfig config = ConfigParser.ParseServerConfigFromFile(SwHomePath + @"\Config\Server.ini");
            if (!string.IsNullOrEmpty(config.FullUrl)) ServerEnvironmentTextBlock.Text = $"{config.FullUrl}";
            else ServerEnvironmentTextBlock.Text = "未找到环境";
            LoadSavedCredentials();
        }

        private void LoadSavedCredentials()
        {
            var (username, password, isRemember) = TokenManager.LoadCredentials();
            if (!string.IsNullOrEmpty(username))
            {
                txtUsername.Text = username;
            }

            if (isRemember && !string.IsNullOrEmpty(password))
            {
                txtPassword.Password = password;
                txtPasswordVisible.Text = password; // Sync visible box too
                chkRememberPassword.IsChecked = true;
            }
        }

        private void chkShowPassword_Checked(object sender, RoutedEventArgs e)
        {
            txtPasswordVisible.Text = txtPassword.Password;
            txtPassword.Visibility = Visibility.Collapsed;
            txtPasswordVisible.Visibility = Visibility.Visible;
        }

        private void chkShowPassword_Unchecked(object sender, RoutedEventArgs e)
        {
            txtPassword.Password = txtPasswordVisible.Text;
            txtPasswordVisible.Visibility = Visibility.Collapsed;
            txtPassword.Visibility = Visibility.Visible;
        }

        private async void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            string username = txtUsername.Text.Trim();
            string password = chkShowPassword.IsChecked == true ? txtPasswordVisible.Text : txtPassword.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                lblStatus.Text = "请输入账号和密码";
                return;
            }

            btnLogin.IsEnabled = false;
            lblStatus.Text = "正在登录...";

            try
            {
                var response = await HttpRequest.login(username, password);
                if (response.isSuccess)
                {
                    SessionContext.Token = response.token;
                    SessionContext.UserId = username;
                    TokenManager.SaveToken(SessionContext.Token);
                    TokenManager.SaveCredentials(username, password, chkRememberPassword.IsChecked == true);

                    this.DialogResult = true;
                    this.Close();
                }
                else
                {
                    lblStatus.Text = "登录失败，请确认账号密码信息";
                    // Do not reset fields on failure as per requirement
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"错误: {ex.Message}";
            }
            finally
            {
                btnLogin.IsEnabled = true;
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}