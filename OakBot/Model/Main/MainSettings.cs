﻿using System;

namespace OakBot.Model
{
    [Serializable]
    public class MainSettings
    {
        public string Channel { get; set; }
        public string BotUsername { get; set; }
        public string BotOauthKey { get; set; }
        public string CasterUsername { get; set; }
        public string CasterOauthKey { get; set; }
        public bool AutoConnectStartup { get; set; }
        public bool UseSecureConnection { get; set; }

        public MainSettings()
        {
            // Default values
            Channel = string.Empty;
            BotUsername = string.Empty;
            BotOauthKey = string.Empty;
            CasterUsername = string.Empty;
            CasterOauthKey = string.Empty;
            AutoConnectStartup = false;
            UseSecureConnection = false;
        }
    }
}
