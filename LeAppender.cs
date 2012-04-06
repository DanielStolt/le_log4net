/*
   Logentries Log4Net Logging agent
   Copyright 2010,2011 Logentries, Jlizard
   Mark Lacomber <marklacomber@gmail.com>
                                            */


﻿using System;
using System.Configuration;
﻿using System.Linq;
﻿using System.Text.RegularExpressions;
﻿using System.Net.Security;
using System.Net.Sockets;
using log4net.Core;

namespace log4net.Appender
{
    public class LeAppender : AppenderSkeleton
    {
        #region Private Instance Fields
        private SslStream sslSock = null;
        private TcpClient leSocket = null;
        private System.Text.ASCIIEncoding encoding;
        private String m_Key;
        private String m_Location;
        private bool m_Debug;
        #endregion

        #region Public Instance Properties

        public string Key
        {
            get { return m_Key; }
            set { m_Key = SubstituteAppSetting(value); }
        }

        public string Location
        {
            get { return m_Location; }
            set { m_Location = SubstituteAppSetting(value); }
        }

        public bool Debug
        {
            get { return m_Debug; }
            set { m_Debug = value; }
        }
        #endregion

        #region Constructor

        public LeAppender()
        {
        }

        #endregion

        private void createSocket(String key, String location)
        {
            this.encoding = new System.Text.ASCIIEncoding();
            this.leSocket = new TcpClient("api.logentries.com", 443);
            this.leSocket.NoDelay = true;
            this.sslSock = new SslStream(this.leSocket.GetStream());

            this.sslSock.AuthenticateAsClient("logentries.com");

            String output = "PUT /" + key + "/hosts/" + location + "/?realtime=1 HTTP/1.1\r\n";
            this.sslSock.Write(this.encoding.GetBytes(output), 0, output.Length);
            output = "Host: api.logentries.com\r\n";
            this.sslSock.Write(this.encoding.GetBytes(output), 0, output.Length);
            output = "Accept-Encoding: identity\r\n";
            this.sslSock.Write(this.encoding.GetBytes(output), 0, output.Length);
            output = "Transfer_Encoding: chunked\r\n\r\n";
            this.sslSock.Write(this.encoding.GetBytes(output), 0, output.Length);
        }

        protected override void Append(LoggingEvent loggingEvent)
        {
            if (this.sslSock == null)
            {
                try
                {
                    this.createSocket(this.Key, this.Location);
                }
                catch (Exception e)
                {
                        WriteDebugMessages("Error connecting to LogEntries", e);
                }
            }

            String final = RenderLoggingEvent(loggingEvent) + "\r\n";

            try
            {
                this.sslSock.Write(this.encoding.GetBytes(final), 0, final.Length);
            }
            catch (Exception e)
            {
                    WriteDebugMessages("Error sending log to LogEntries", e);
                try
                {
                    this.createSocket(this.Key, this.Location);
                    this.sslSock.Write(this.encoding.GetBytes(final), 0, final.Length);
                }
                catch (Exception ex)
                {
                        WriteDebugMessages("Error sending log to LogEntries", ex);
                }
            }
        }

        protected override void Append(LoggingEvent[] loggingEvents)
        {
            if (this.sslSock == null)
            {
                try
                {
                    this.createSocket(this.Key, this.Location);
                }
                catch (SocketException e)
                {
                        WriteDebugMessages("Error connecting to LogEntries", e);
                }
            }
            foreach (LoggingEvent logEvent in loggingEvents)
            {
                this.Append(logEvent);
            }
        }

        protected override bool RequiresLayout
        {
            get { return true; }
        }

        private void WriteDebugMessages(string message, Exception e)
        {
            if (!Debug) return;
            string[] messages = {message, e.ToString()};
            foreach (var msg in messages)
            {
                System.Diagnostics.Debug.WriteLine(msg);
                Console.Error.WriteLine(msg);
            }
        }

        private static string SubstituteAppSetting(string potentialKey)
        {
            /* This method isn't working, temporary fix below
             * 
            var isWrappedPattern = new Regex(@"^\$AppSetting\{(.*)\}$");

            var matches = isWrappedPattern.Matches(potentialKey);
            if (matches.Count == 1)
            {
                var settingKey = matches[0].Groups[1].Value;
                var appSettings = ConfigurationManager.AppSettings;
                if (appSettings.HasKeys() && appSettings.AllKeys.Contains(settingKey))
                {
                    return appSettings[settingKey];
                }
            }
            return potentialKey;
             */
            var appSettings = ConfigurationManager.AppSettings;
            if (appSettings.HasKeys() && appSettings.AllKeys.Contains(potentialKey))
            {
                return appSettings[potentialKey];
            }
            else
            {
                return potentialKey;
            }
        }
    }
}