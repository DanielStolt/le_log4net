// 
// Copyright (c) 2010-2012 Logentries, Jlizard
// 
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without 
// modification, are permitted provided that the following conditions 
// are met:
// 
// * Redistributions of source code must retain the above copyright notice, 
//   this list of conditions and the following disclaimer. 
// 
// * Redistributions in binary form must reproduce the above copyright notice,
//   this list of conditions and the following disclaimer in the documentation
//   and/or other materials provided with the distribution. 
// 
// * Neither the name of Logentries nor the names of its 
//   contributors may be used to endorse or promote products derived from this
//   software without specific prior written permission. 
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE 
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE 
// ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE 
// LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR 
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN 
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) 
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF 
// THE POSSIBILITY OF SUCH DAMAGE.
// 
// Mark Lacomber <marklacomber@gmail.com>
// Viliam Holub <vilda@logentries.com>

/*
 *   VERSION:  2.3.2
 */


﻿using System;
using System.Collections.Concurrent;
using System.Configuration;
﻿using System.Linq;
﻿using System.Text.RegularExpressions;
using System.Text;
﻿using System.Net.Security;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using log4net.Core;
using log4net.Util;

namespace log4net.Appender
{
    public class LogentriesAppender : AppenderSkeleton
    {
        /*
         * Constants
         */

        /** Current version number  */
        public const String VERSION = "2.3.2";
        /** Size of the internal event queue. */
        const int QUEUE_SIZE = 32768;
        /** Logentries API server address. */
        const String LE_API = "api.logentries.com";
        /** Port number for token logging on Logentries API server. */
        const int LE_TOKEN_PORT = 10000;
        /** Port number for http PUT logging on Logentries API server. */
        const int LE_HTTP_PORT = 80;
        /** UTF-8 output character set. */
        static readonly UTF8Encoding UTF8 = new UTF8Encoding();
        /** ASCII character set used by HTTP. */
        static readonly ASCIIEncoding ASCII = new ASCIIEncoding();
        /** Minimal delay between attempts to reconnect in milliseconds. */
        const int MIN_DELAY = 100;
        /** Maximal delay between attempts to reconnect in milliseconds. */
        const int MAX_DELAY = 10000;
        /** LE appender signature - used for debugging messages. */
        const String LE = "LE: ";
        /** Logentries Config Token */
        const String CONFIG_TOKEN = "LOGENTRIES_TOKEN";
        /** Logentries Config Account Key */
        const String CONFIG_ACCOUNT_KEY = "LOGENTRIES_ACCOUNT_KEY";
        /** Logentries Config Location */
        const String CONFIG_LOCATION = "LOGENTRIES_LOCATION";
        /** Error message displayed when invalid token is detected. */
        const String INVALID_TOKEN = "\n\nIt appears your LOGENTRIES_TOKEN parameter in web/app.config is invalid!\n\n";
        /** Error message displayed when invalid account_key or location parameters are detected */
        const String INVALID_HTTP_PUT = "\n\nIt appears your LOGENTRIES_ACCOUNT_KEY or LOGENTRIES_LOCATION parameters in web/app.config are invalid!\n\n";

        readonly Random random = new Random();

        /** Custom socket class to allow for choice of Token-based logging and HTTP PUT */
        private LogentriesTcpClient tcp_client = null;
        /** Thread used for background polling of log queue */
        public Thread thread;
        /** Asynchronous logging started flag */
        public bool started = false;
        /** Logentries Token Parameter */
        private String m_Token = "";
        /** Logentries Account Key Parameter */
        private String m_Key = "";
        /** Logentries Location Parameter */
        private String m_Location = "";
        /** Logentries HTTP PUT flag parameter */
        private bool m_HttpPut = false;
        /** Message Queue. */
        public BlockingCollection<string> queue;
        /** Newline char to trim from message for formatting */
        static char[] trimChars = { '\n' };
        /** Logentries Debug flag parameter */
        private bool m_Debug;

        #region Public Instance Properties

        /** Debug flag. */
        public bool Debug
        {
            get { return m_Debug; }
            set { m_Debug = value; }
        }

        /** Option to set Token programmatically or in Appender Definition */
        public string Token
        {
            get { return m_Token; }
            set { m_Token = value; }
        }

        /** HTTP PUT Flag */
        public bool HttpPut
        {
            get { return m_HttpPut; }
            set { m_HttpPut = value; }
        }

        /** ACCOUNT_KEY parameter for HTTP PUT logging */
        public String Key
        {
            get { return m_Key; }
            set { m_Key = value; }
        }

        /** LOCATION parameter for HTTP PUT logging */
        public String Location
        {
            get { return m_Location; }
            set { m_Location = value; }
        }

        #endregion

        #region Constructor

        public LogentriesAppender()
        {
            queue = new BlockingCollection<string>(QUEUE_SIZE);

            thread = new Thread(new ThreadStart(run_loop));
            thread.Name = "Logentries Log4net Appender";
            thread.IsBackground = true;
        }

        #endregion

        private void openConnection()
        {
            try
            {
                if (this.tcp_client == null)
                    this.tcp_client = new LogentriesTcpClient(HttpPut);

                this.tcp_client.Connect();

                if (HttpPut)
                {
                    String header = String.Format("PUT /{0}/hosts/{1}/?realtime=1 HTTP/1.1\r\n\r\n", this.m_Key, this.m_Location);
                    this.tcp_client.Write(ASCII.GetBytes(header), 0, header.Length);
                }
            }
            catch
            {
                throw new IOException();
            }
        }

        private void reopenConnection()
        {
            closeConnection();

            int root_delay = MIN_DELAY;
            while (true)
            {
                try
                {
                    openConnection();

                    return;
                }
                catch (Exception e)
                {
                    if (Debug)
                    {
                        WriteDebugMessages("Unable to connect to Logentries");
                    }
                }

                root_delay *= 2;
                if (root_delay > MAX_DELAY)
                    root_delay = MAX_DELAY;
                int wait_for = root_delay + random.Next(root_delay);

                try
                {
                    Thread.Sleep(wait_for);
                }
                catch
                {
                    throw new ThreadInterruptedException();
                }
            }
        }

        private void closeConnection()
        {
            if (this.tcp_client != null)
                this.tcp_client.Close();
        }

        public void run_loop()
        {
            try
            {
                //Open connection
                reopenConnection();

                //Send data in queue
                while (true)
                {
                    //Take data from queue
                    string line = queue.Take();

                    string final_line = (!HttpPut ? this.Token + line : line) + '\n';

                    byte[] data = LogentriesAppender.UTF8.GetBytes(final_line);

                    //Send data, reconnect if needed
                    while (true)
                    {
                        try
                        {
                            this.tcp_client.Write(data, 0, data.Length);
                        }
                        catch (IOException e)
                        {
                            //Reopen the lost connection
                            reopenConnection();
                            continue;
                        }
                        break;
                    }
                }
            }
            catch (ThreadInterruptedException e)
            {
                WriteDebugMessages("Logentries asynchronous socket client interrupted");
            }
        }

        public void addLine(String line)
        {
            WriteDebugMessages("Queueing " + line);

            //Try to append data to queue
            bool is_full = !queue.TryAdd(line);

            //If its full, remove latest item and try again
            if (is_full)
            {
                queue.Take();
                queue.TryAdd(line);
            }
        }

        protected override void Append(LoggingEvent loggingEvent)
        {
            if (!started && checkCredentials())
            {
                WriteDebugMessages("Starting Logentries asynchronous socket client");
                thread.Start();
                started = true;
            }

            //Render message content
            String renderedEvent = RenderLoggingEvent(loggingEvent);

            renderedEvent = renderedEvent.TrimEnd(trimChars);

            //Replace newline with line separator to maintain formatting
            renderedEvent = renderedEvent.Replace('\n', '\u2028');

            addLine(renderedEvent);

        }

        protected override void Append(LoggingEvent[] loggingEvents)
        {
            foreach (LoggingEvent logEvent in loggingEvents)
            {
                this.Append(logEvent);
            }
        }

        protected override bool RequiresLayout
        {
            get { return true; }
        }

        protected override void OnClose()
        {
            thread.Interrupt();
        }

        public bool checkCredentials()
        {
            var appSettings = ConfigurationManager.AppSettings;

            if (!HttpPut)
            {
                if (checkValidUUID(this.m_Token))
                    return true;

                if (appSettings.AllKeys.Contains(CONFIG_TOKEN) && checkValidUUID(appSettings[CONFIG_TOKEN]))
                {
                    this.m_Token = appSettings[CONFIG_TOKEN];
                    return true;
                }

                WriteDebugMessages(INVALID_TOKEN);
                return false;
            }

            if (this.m_Key != "" && checkValidUUID(this.m_Key) && this.m_Location != "")
                return true;

            if (appSettings.AllKeys.Contains(CONFIG_ACCOUNT_KEY) && checkValidUUID(appSettings[CONFIG_ACCOUNT_KEY]))
            {
                this.m_Key = appSettings[CONFIG_ACCOUNT_KEY];

                if (appSettings.AllKeys.Contains(CONFIG_LOCATION) && appSettings[CONFIG_LOCATION] != "")
                {
                    this.m_Location = appSettings[CONFIG_LOCATION];
                    return true;
                }
            }

            WriteDebugMessages(INVALID_HTTP_PUT);
            return false;
        }

        public bool checkValidUUID(string uuid_input)
        {
            if (uuid_input == "")
                return false;

            System.Guid newGuid = System.Guid.NewGuid();

            return System.Guid.TryParse(uuid_input, out newGuid);
        }
		
		//Used for UnitTests, Append method is protected
		public void TestAppend(LoggingEvent logEvent)
		{
			this.Append(logEvent);
		}

        private void WriteDebugMessages(string message, Exception e)
        {
            message = LE + message;
            if (!Debug) 
               return;
            string[] messages = {message, e.ToString()};
            foreach (var msg in messages)
            {
                LogLog.Debug(typeof(LogentriesAppender), msg);
            }
        }

        private void WriteDebugMessages(string message)
        {
            if (!Debug)
                return;

            message = LE + message;

	        LogLog.Debug(typeof(LogentriesAppender), message);
        }

        private string SubstituteAppSetting(string key)
        {
            var appSettings = ConfigurationManager.AppSettings;
            if (appSettings.HasKeys() && appSettings.AllKeys.Contains(key))
            {
                return appSettings[key];
            }
            else
            {
                return key;
            }
        }

        /** Custom Class to support both HTTP PUT and Token-based logging */
        private class LogentriesTcpClient
        {
            private TcpClient client = null;
            private Stream stream = null;
            private int port;

            public LogentriesTcpClient(bool httpPut)
            {
                port = httpPut ? LE_HTTP_PORT : LE_TOKEN_PORT;
            }

            public void Connect()
            {
                this.client = new TcpClient(LE_API, port);
                this.client.NoDelay = true;

                this.stream = client.GetStream();
            }

            public void Write(byte[] buffer, int offset, int count)
            {
                this.stream.Write(buffer, offset, count);
            }

            public void Flush()
            {
                this.stream.Flush();
            }

            public void Close()
            {
                if (this.client != null)
                {
                    try
                    {
                        this.client.Close();
                    }
                    catch { }
                }
            }
        }
    }
}
