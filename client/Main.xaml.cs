﻿using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Windows.Input;
using System.Xml;
using System.Threading;
using System.Net;
using System.Web;

namespace client
{
    /// <summary>
    /// Логика взаимодействия для Window1.xaml
    /// </summary>
    public partial class Main : Window
    {
        public static bool logging = true;
        public static bool debug = false;
        public static string version = string.Empty;
        public static bool staticFiles = false;
        public static bool collect = false;
        public static bool skipCheck = false;
        public static string lang = string.Empty;
        public static bool auto = false;
        public static CookieCollection _cookies;
        private string _region = string.Empty;
        private int _regionUid;
        public static Arguments cmd;

        public Main()
        {
 
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
            System.Net.ServicePointManager.Expect100Continue = false;
            InitializeComponent();
            ReadSettings();
            cmd = new Arguments(Environment.GetCommandLineArgs());
            if (cmd["collect"] != null)
                collect = true;
            if (cmd["skip"] != null)
                skipCheck = true;
            if (cmd["debug"] != null)
                debug = true;
            if (cmd["lang"] != null)
            {
                if (Servers._langs.ContainsKey(cmd["lang"]))
                    lang = Servers._langs[cmd["lang"]];
            }
            if (cmd["autologin"] != null)
            {
                auto = true;
                new Thread(AutoLogin) { IsBackground = true }.Start();
            }
            if (cmd["login"] != null && cmd["password"] != null)
            {
                login.Text = cmd["login"];
                password.Password = cmd["password"];
                pwd.Visibility = System.Windows.Visibility.Collapsed;
                new Thread(AutoLogin) { IsBackground = true }.Start();
            }
            butt.IsEnabled = false;
            new Thread(checkVersion) { IsBackground = true }.Start();
        }

        public static string ClientDirectory
        {
            get
            {
                return System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tso_portable");
            }
        }
        private const string _chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private static Random _rng = new Random();
        public static string RandomString
        {
            get
            {

                char[] buffer = new char[5];
                for (int i = 0; i < 5; i++)
                {
                    buffer[i] = _chars[_rng.Next(_chars.Length)];
                }
                return new string(buffer);
            }
        }

        private void AutoLogin()
        {
            Thread.Sleep(1);
            Dispatcher.BeginInvoke(new ThreadStart(delegate { butt_Click_1(null, null); }));
        }

        public void checkVersion()
        {
            if (skipCheck)
            {
                Dispatcher.BeginInvoke(new ThreadStart(delegate { error.Text = "можно играть"; butt.IsEnabled = true; }));
                return;
            }
            try
            {
                PostSubmitter post;
                Dispatcher.BeginInvoke(new ThreadStart(delegate { error.Text = "Проверяем клиент"; }));
                if (!Directory.Exists(ClientDirectory))
                {
                    Directory.CreateDirectory(ClientDirectory);
                    using (var unzip = new Unzip(new MemoryStream(Properties.Resources.content)))
                    {
                        unzip.ExtractToDirectory(ClientDirectory);
                    }
                }
                Dispatcher.BeginInvoke(new ThreadStart(delegate { error.Text = "Проверяем клиент"; }));
                string chksum = string.Empty;
                bool needDownload = false;
                if (File.Exists(System.IO.Path.Combine(ClientDirectory, "client.swf")))
                    chksum = BitConverter.ToString(System.Security.Cryptography.SHA1.Create().ComputeHash(File.OpenRead(System.IO.Path.Combine(ClientDirectory, "client.swf")))).ToLower().Replace("-", "");
                else
                    needDownload = true;
                if(!string.IsNullOrEmpty(chksum))
                {
                    post = new PostSubmitter
                    {
                        Url = "https://sirris.tsomaps.com/client.swf.sum",
                        Type = PostSubmitter.PostTypeEnum.Get
                    };
                    string rchksum = post.Post(ref _cookies).Trim();
                    if (chksum != rchksum)
                        needDownload = true;
                }
                if (needDownload)
                {
                    Dispatcher.BeginInvoke(new ThreadStart(delegate { error.Text = "Скачиваем.."; }));
                    byte[] client = DownloadFile("https://sirris.tsomaps.com/client.swf");
                    File.WriteAllBytes(System.IO.Path.Combine(ClientDirectory, "client.swf"), client);
                }
                Dispatcher.BeginInvoke(new ThreadStart(delegate { error.Text = "можно играть"; butt.IsEnabled = true; }));
                return;
            } catch (Exception e)
            {
                MessageBox.Show(e.Message + e.StackTrace);
            }
            return;
        }
        public byte[] DownloadFile(string remoteFilename)
        {
            int bytesProcessed = 0;
            Stream remoteStream = null;
            WebResponse response = null;
            List<byte> resultArray = new List<byte>();
            try
            {
                var webProxy = WebProxy.GetDefaultProxy();
                webProxy.UseDefaultCredentials = true;
                WebRequest.DefaultWebProxy = webProxy;
                WebRequest.DefaultWebProxy.Credentials = CredentialCache.DefaultNetworkCredentials;
                WebRequest request = WebRequest.Create(remoteFilename);
                request.Method = "GET";
                if (request != null)
                {
                    response = request.GetResponse();
                    if (response != null)
                    {
                        remoteStream = response.GetResponseStream();
                        byte[] buffer = new byte[4096];
                        long bytesTotal = response.ContentLength;
                        int bytesRead;
                        do
                        {
                            bytesRead = remoteStream.Read(buffer, 0, buffer.Length);
                            byte[] Buf = new byte[bytesRead];
                            Buffer.BlockCopy(buffer, 0, Buf, 0, bytesRead);
                            resultArray.AddRange(Buf);
                            bytesProcessed += bytesRead;
                            Dispatcher.BeginInvoke(new ThreadStart(delegate { error.Text = string.Format("Скачиваем.. {0}%", (100 * bytesProcessed / bytesTotal).ToString()); }));
                        } while (bytesRead > 0);
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
            finally
            {
                if (response != null) response.Close();
                if (remoteStream != null) remoteStream.Close();
            }

            return resultArray.ToArray();
        }

        public void ReadSettings()
        {
            if (File.Exists("settings.dat"))
            {
                try
                {
                    Dispatcher.BeginInvoke(new ThreadStart(delegate
                    {
                        string[] settings = new Crypt().Decrypt(File.ReadAllText("settings.dat"), true).Split(new[] { '|' }, StringSplitOptions.None);
                        //MessageBox.Show("set from func");
                        if(!string.IsNullOrEmpty(settings[0]) && cmd["login"] == null) login.Text = settings[0];
                        if (!string.IsNullOrEmpty(settings[1]) && cmd["password"] == null) password.Password = settings[1];
                        if (!string.IsNullOrEmpty(settings[1])) pwd.Visibility = System.Windows.Visibility.Collapsed;
                        chatEnable.IsChecked = bool.Parse(settings[2]);
                        if (settings.Length > 3)
                        {
                            try
                            {
                                logging = bool.Parse(settings[3]);
                                if (settings.Length > 5)
                                {
                                    _regionUid = string.IsNullOrEmpty(settings[5]) ? 16 : int.Parse(settings[5].Trim());
                                    Dispatcher.BeginInvoke(new ThreadStart(delegate
                                    {
                                        region_list.SelectedIndex = _regionUid;
                                        _region = (region_list.SelectedItem as ComboBoxItem).Tag.ToString();
                                    }));
                                }
                            }
                            catch { }
                        }
                    }));
                }
                catch
                {
                    File.Delete("settings.dat");
                }
            }
        }

        void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            ExceptionDumper.DumpException(e.ExceptionObject as Exception);
        }

        private void titleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void butt_Click(object sender, RoutedEventArgs e)
        {
            App.Current.Shutdown();
        }

        private void password_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if((sender as TextBox).Text == "Электронный адрес" || (sender as TextBox).Text == "Пароль")
            {
                (sender as TextBox).Text = "";
            }

        }

        private void login_LostFocus(object sender, RoutedEventArgs e)
        {
            if ((sender as TextBox).Text == "") (sender as TextBox).Text = "Электронный адрес";
        }

        private void butt_Click_1(object sender, RoutedEventArgs e)
        {
            string error_msg = string.Empty;
            if (string.IsNullOrEmpty(login.Text.Trim()) || login.Text.Trim() == "Электронный адрес") error_msg = "Логин пуст.";
            if (string.IsNullOrEmpty(password.Password.Trim()) || password.Password.Trim() == "Пароль")
            {
                if (string.IsNullOrEmpty(error_msg)) error_msg = "Пароль пуст.";
            }
            if (!string.IsNullOrEmpty(error_msg))
            {
                
                error.Text = error_msg;
                return;
            }
            error.Text = string.Empty;
            if (SaveLogin.IsChecked == true)
            {
                File.WriteAllText("settings.dat", new Crypt().Encrypt(string.Format("{0}|{1}|{2}|{3}|{4}|{5}|", login.Text, password.Password, chatEnable.IsChecked.ToString(), "0", "0", _regionUid), true));
            }
            else
            {
                File.WriteAllText("settings.dat", new Crypt().Encrypt(string.Format("||{0}|{1}|{2}|{3}|", chatEnable.IsChecked.ToString(), "0", "0", _regionUid), true));
            }
            this.Visibility = System.Windows.Visibility.Hidden;
            bool collections = false;
            if (!auto)
            {
                MessageBoxResult messageBoxResult = System.Windows.MessageBox.Show("                  Использовать подсветку коллекций?\nНажимайте \"да\" только если у вас запущен UbiCollect.exe!", "Подсветка", System.Windows.MessageBoxButton.YesNo);
                if (messageBoxResult == MessageBoxResult.Yes)
                {
                    collections = true;
                }
            }
            if(collect)
                collections = true;
            login log = new login() { Owner = ((null == e) ? null : this), username = login.Text, password = password.Password, region = _region, WindowStartupLocation = ((null == e) ? System.Windows.WindowStartupLocation.CenterScreen : System.Windows.WindowStartupLocation.CenterOwner) };
            log.ShowDialog();
            if (log.DialogResult == true)
            {
                _cookies = log.Cookies;
                var tsoUrl = HttpUtility.ParseQueryString(log.Ver);
                if (collections)
                    tsoUrl.Set("s", "http://127.0.0.1:9000/https://ubistatic-a.akamaihd.net/0018/live");
                if (!string.IsNullOrEmpty(lang))
                    tsoUrl.Set("lang", lang);
                string tsoArg = string.Format("tso://{0}&baseUri={1}", tsoUrl.ToString().Replace("bb=https", "bb=http").Replace(":443", ""), Servers._servers[_region].domain);
                XmlDocument Doc = new XmlDocument();
                XmlNamespaceManager ns = new XmlNamespaceManager(Doc.NameTable);
                ns.AddNamespace("adobe", "http://ns.adobe.com/air/application/15.0");
                Doc.Load(string.Format("{0}\\META-INF\\AIR\\application.xml", ClientDirectory));
                Doc.SelectSingleNode("/adobe:application/adobe:id", ns).InnerText = "TSO-" + RandomString;
                Doc.SelectSingleNode("/adobe:application/adobe:name", ns).InnerText = "The Settlers Online - " + log.nickName;
                Doc.Save(string.Format("{0}\\META-INF\\AIR\\application.xml", ClientDirectory));
                System.Diagnostics.Process.Start(string.Format("{0}\\client.exe", ClientDirectory), tsoArg);
                try
                {
                    App.Current.Shutdown(1);
                }
                catch { }
            }
            else
            {

            }
            this.Visibility = System.Windows.Visibility.Visible;
        }

        private void pwd_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            (sender as TextBox).Visibility = System.Windows.Visibility.Collapsed;
            password.Focus();
            
        }

        private void password_LostFocus(object sender, RoutedEventArgs e)
        {
            if ((sender as PasswordBox).Password == "") pwd.Visibility = System.Windows.Visibility.Visible;
        }

        private void password_GotFocus(object sender, RoutedEventArgs e)
        {
            pwd.Visibility = System.Windows.Visibility.Collapsed;
            password.SelectAll();
        }

        private void login_GotFocus(object sender, RoutedEventArgs e)
        {
            if ((sender as TextBox).Text == "Электронный адрес" || (sender as TextBox).Text == "Пароль")
            {
                (sender as TextBox).Text = "";
            }
        }

        private void password_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                butt_Click_1(null, null);
            }
        }


        private void Region_list_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _region = ((sender as ComboBox).SelectedItem as ComboBoxItem).Tag.ToString();
            _regionUid = int.Parse(((sender as ComboBox).SelectedItem as ComboBoxItem).Uid);
        }
    }
}
