﻿using System;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Threading;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Runtime.Serialization.Json;
using System.Runtime.Serialization;

namespace client
{
    /// <summary>
    /// Логика взаимодействия для login.xaml
    /// </summary>
    public partial class login : Window
    {
        public string username { get; set; }
        public string password { get; set; }
        public string region { get; set; }
        public string nickName { get; set; }
        public string Ver { get; set; }
        public string Session { get; set; }
        public CookieCollection Cookies { get; set; }
        private Thread authlogin;
        public int attepts = 0;


        public login()
        {
            InitializeComponent();
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
            System.Net.ServicePointManager.ServerCertificateValidationCallback
                = delegate (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
                {
                    return true;
                };
            System.Net.ServicePointManager.Expect100Continue = false;
            Closing += new System.ComponentModel.CancelEventHandler(login_Closing);
            authlogin = new Thread(MainAuth) { IsBackground = true };
            authlogin.Start();
        }

        void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            ExceptionDumper.DumpException(e.ExceptionObject as Exception);
        }

        void login_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try { authlogin.Abort(); }
            catch { }
            GC.Collect();
        }

        private void titleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        public void AddToRich(string message)
        {
            Dispatcher.BeginInvoke(new ThreadStart(delegate
            {
                richTextBox1.AppendText(message + "\r");
            }));
        }

        private void richTextBox1_TextChanged(object sender, TextChangedEventArgs e)
        {
            richTextBox1.ScrollToEnd();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try { authlogin.Abort(); }
            catch { }
            DialogResult = false;
        }
        public T Deserialize<T>(string aJSON) where T : new()
        {
            T deserializedObj = new T();
            MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(aJSON));
            DataContractJsonSerializer ser = new DataContractJsonSerializer(deserializedObj.GetType());
            deserializedObj = (T)ser.ReadObject(ms);
            ms.Close();
            return deserializedObj;
        }

        public void MainAuth()
        {
            try
            {
                PostSubmitter post;
                string res;
                CookieCollection _cookies = new CookieCollection();
                if (attepts > 5)
                {
                    AddToRich("Хватит пытаться :)");
                    return;
                }
                AddToRich("Попытка авторизации #" + attepts++);
                post = new PostSubmitter
                {
                    Url = "https://public-ubiservices.ubi.com/v3/profiles/sessions",
                    Type = PostSubmitter.PostTypeEnum.Post
                };
                post.HeaderItems.Add("Authorization", "Basic " + Convert.ToBase64String(UTF8Encoding.UTF8.GetBytes(string.Format("{0}:{1}", username.Trim(), password.Trim()))));
                post.ContentType = "application/json";
                post.HeaderItems.Add("Ubi-AppId", "39164658-8187-4bf4-b46c-375f68356e3b");
                post.HeaderItems.Add("Ubi-RequestedPlatformType", "uplay");
                post.HeaderItems.Add("GenomeId", "978da00d-2533-4af4-a550-3ba09289084e");
                res = post.Post(ref _cookies);
                AddToRich("Авторизация ubi");
                if (res.Contains("sessionKey"))
                {
                    AddToRich("Успешная авторизация ");
                    UbiSession sessionData = Deserialize<UbiSession>(res);
                    post = new PostSubmitter
                    {
                        Url = string.Format("{0}{1}", Servers._servers[region].domain, Servers._servers[region].uplay),
                        Type = PostSubmitter.PostTypeEnum.Post
                    };
                    DateTime dt = DateTime.Parse(sessionData.expiration).ToLocalTime();
                    Int32 unixTimestamp = (Int32)(dt.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                    post.PostItems.Add("id", sessionData.userId);
                    post.PostItems.Add("ticket", sessionData.ticket);
                    post.PostItems.Add("expiration", unixTimestamp.ToString());
                    post.PostItems.Add("undefined", sessionData.sessionId);
                    post.PostItems.Add("activated", "true");
                    AddToRich("Авторизация uplay");
                    res = post.Post(ref _cookies);
                    if (Main.debug)
                        File.AppendAllText("debug.txt", "recieved "+ res + "\n");
                    if (res.Contains("OKAY"))
                    {
                        AddToRich("Успешная авторизация ");
                        post = new PostSubmitter
                        {
                            Url = string.Format("{0}{1}", Servers._servers[region].domain, Servers._servers[region].main),
                            Type = PostSubmitter.PostTypeEnum.Post
                        };
                        post.PostItems.Add("start", "1");
                        res = post.Post(ref _cookies);
                        post = new PostSubmitter
                        {
                            Url = string.Format("{0}{1}", Servers._servers[region].domain, Servers._servers[region].play),
                            Type = PostSubmitter.PostTypeEnum.Get
                        };
                        AddToRich("Запрос страницы play");
                        string token = post.Post(ref _cookies);
                        if (!token.Contains("thisProgram"))
                        {
                            AddToRich("Не смогли получить печеньки :'(");
                            return;
                        }
                        if (!PrepareFlash(token))
                        {
                            AddToRich("Ошибка получения параметров");
                            MainAuth();
                        }
                        foreach (Cookie cook in _cookies)
                        {
                            if (cook.Name.Contains("SESS"))
                            {
                                Session = cook.Value;
                                break;
                            }
                        }
                        Cookies = _cookies;
                        return;
                    }
                    else
                    {
                        checkForError(res);
                    }
                }
                else
                {
                    checkForError(res);
                }
            }
            catch (Exception e)
            {
                string msg = e.Message;
                if (Main.debug)
                    msg += e.StackTrace;
                AddToRich("Ошибка авторизации. ответ - " + msg);
                MainAuth();
            }
            return;
        }

        private void checkForError(string res)
        {
            if (res.Contains("FAILED"))
            {
                AddToRich("Логин/пароль неверны.");
                return;
            }
            if (res.Contains(" CAPCHA "))
            {
                AddToRich("Поймали капчу.. попробуйте позже.");
                return;
            }
            if (res.Contains("UPLAY"))
            {
                AddToRich("UPLAY не отвечает.");
                MainAuth();
            }
            if (res.Contains("EXCEPTION"))
            {
                AddToRich("Ошибка на странице авторизации.");
                MainAuth();
            }

            if (res.Trim() == "")
            {
                AddToRich("Хм, пустой ответ.. странно ");
            }
            else AddToRich("Ошибка авторизации. ответ - " + res);
        }

        public bool PrepareFlash(string htmlPage)
        {
            AddToRich("Получаем параметры.");
            Match match = Regex.Match(htmlPage, "thisProgram: \"(?<tso>.*)\"", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                AddToRich("Ошибка получения кода.");
                return false;
            }
            else
            {
                Ver = match.Groups["tso"].Value.Trim();
            }
            match = Regex.Match(htmlPage, "loggedInUserName = '([А-Яа-яёЁйЙA-Za-z0-9_-]+)';", RegexOptions.IgnoreCase);
            nickName = username;
            if (match.Success)
            {
                nickName = match.Groups[1].Value.Trim();
                AddToRich("Ник игрока - " + nickName);
            }
            AddToRich("Запускаем клиент...");
            Thread.Sleep(1000);
            Dispatcher.BeginInvoke(new ThreadStart(delegate
            {
                try
                {
                    DialogResult = true;
                }
                catch { }
            }));
            return true;

        }
    }

    [DataContract]
    public class UbiSession
    {
        [DataMember(Name = "platformType")]
        public string platformType { get; set; }
        [DataMember(Name = "ticket")]
        public string ticket { get; set; }
        [DataMember(Name = "twoFactorAuthenticationTicket")]
        public string twoFactorAuthenticationTicket { get; set; }
        [DataMember(Name = "profileId")]
        public string profileId { get; set; }
        [DataMember(Name = "userId")]
        public string userId { get; set; }
        [DataMember(Name = "nameOnPlatform")]
        public string nameOnPlatform { get; set; }
        [DataMember(Name = "environment")]
        public string environment { get; set; }
        [DataMember(Name = "expiration")]
        public string expiration { get; set; }
        [DataMember(Name = "spaceId")]
        public string spaceId { get; set; }
        [DataMember(Name = "sessionId")]
        public string sessionId { get; set; }
        [DataMember(Name = "sessionKey")]
        public string sessionKey { get; set; }
        [DataMember(Name = "rememberMeTicket")]
        public string rememberMeTicket { get; set; }

    }
}
