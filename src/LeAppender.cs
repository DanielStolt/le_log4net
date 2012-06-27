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

namespace log4net.Appender
{
    public class LeAppender : AppenderSkeleton
    {
        /*
         * Constants
         */

        /** Size of the internal event queue. */
        public static readonly int QUEUE_SIZE = 32768;
        /** Logentries API server address. */
        static readonly String LE_API = "api.logentries.com";
        /** Logentries Client Authentication */
        static readonly String LE_AUTH_CLIENT = "logentries.com";
        /** Default port number for Logentries API server. */
        static readonly int LE_PORT = 80;
        /** Default SSL port number for Logentries API server. */
        static readonly int LE_SSL_PORT = 443;
        /** UTF-8 output character set. */
        static readonly UTF8Encoding UTF8 = new UTF8Encoding();
        /** ASCII character set used by HTTP. */
        static readonly ASCIIEncoding ASCII = new ASCIIEncoding();
        /** Minimal delay between attempts to reconnect in milliseconds. */
        static readonly int MIN_DELAY = 100;
        /** Maximal delay between attempts to reconnect in milliseconds. */
        static readonly int MAX_DELAY = 10000;
        /** LE appender signature - used for debugging messages. */
        static readonly String LE = "LE: ";
        /** Logentries Config Key */
        static readonly String CONFIG_KEY = "LOGENTRIES_ACCOUNT_KEY";
        /** Logentries Config Location */
        static readonly String CONFIG_LOCATION = "LOGENTRIES_LOCATION";
        /** Error message displayed when wrong configuration has been detected. */
        static readonly String WRONG_CONFIG = "\n\nIt appears you forgot to customize your web.config file!\n\n";

        readonly Random random = new Random();

        private MyTcpClient socket = null;
        public Thread thread;
        public bool started = false;
        /** Message Queue */
        public BlockingCollection<Byte[]> queue;

        /** Logentries Parameters */
        private String m_Key;
        private String m_Location;
        private bool m_Debug;
        private bool m_Ssl;

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

        public bool Ssl
        {
            get { return m_Ssl; }
            set { m_Ssl = value; }
        }
        #endregion

        #region Constructor

        public LeAppender()
        {
            queue = new BlockingCollection<byte[]>(QUEUE_SIZE);

            thread = new Thread(new ThreadStart(run_loop));
            thread.Name = "Logentries Log4net Appender";
            thread.IsBackground = true;
        }

        #endregion

        private void openConnection()
        {
            String api_addr = LE_API;

            try
            {
                this.socket = new MyTcpClient(LE_API, this.Ssl);

                String header = String.Format("PUT /{0}/hosts/{1}/?realtime=1 HTTP/1.1\r\n\r\n", SubstituteAppSetting(Key), SubstituteAppSetting(Location));
                this.socket.Write(ASCII.GetBytes(header), 0, header.Length);
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
            if (this.socket != null)
                this.socket.Close();
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
                    byte[] data = queue.Take();

                    //Send data, reconnect if needed
                    while (true)
                    {
                        try
                        {
                            socket.Write(data, 0, data.Length);
                            socket.Flush();
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
                WriteDebugMessages("Asynchronous socket client interrupted");
            }
        }

        public void addLine(String line)
        {
            WriteDebugMessages("Queueing " + line);

            byte[] data = UTF8.GetBytes(line+'\n');

            //Try to append data to queue
            bool is_full = !queue.TryAdd(data);

            //If its full, remove latest item and try again
            if (is_full)
            {
                queue.Take();
                queue.TryAdd(data);
            }
        }

        protected override void Append(LoggingEvent loggingEvent)
        {
            if (!checkCredentials())
            {
                WriteDebugMessages(WRONG_CONFIG);
                return;
            }
            if (!started)
            {
                WriteDebugMessages("Starting asynchronous socket logging");
                thread.Start();
                started = true;
            }

            //Append message content
            addLine(RenderLoggingEvent(loggingEvent));
 
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
            if (!appSettings.AllKeys.Contains(CONFIG_KEY) || !appSettings.AllKeys.Contains(CONFIG_LOCATION))
                return false;
            if (appSettings[CONFIG_KEY] == "" || appSettings[CONFIG_LOCATION] == "")
                return false;

            return true;
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
                LogLog.Debug(typeof(LeAppender), msg);
            }
        }

        private void WriteDebugMessages(string message)
        {
            message = LE + message;
            if (!Debug) 
               return;
            
	    LogLog.Debug(typeof(LeAppender), msg);
        }

        private static string SubstituteAppSetting(string potentialKey)
        {
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

        //Custom class to differentiate between Stream and SslStream
        //as they don't share a common base class in C#
        private class MyTcpClient
        {
            private TcpClient client = null;
            private Stream stream = null;
            private SslStream stream_ssl = null;
            private bool ssl_choice;

            public MyTcpClient(String host, bool Ssl)
            {
                int port = Ssl ? LE_SSL_PORT : LE_PORT;
                client = new TcpClient(host, port);
                client.NoDelay = true;
                ssl_choice = Ssl;
                this.stream = client.GetStream();

                if (Ssl)
                {
                    this.stream_ssl = new SslStream(this.stream);
                    this.stream_ssl.AuthenticateAsClient(LE_AUTH_CLIENT);
                }
            }

            public void Write(byte[] buffer, int offset, int count)
            {
                if (ssl_choice)
                {
                    this.stream_ssl.Write(buffer, offset, count);
                }
                else
                {
                    this.stream.Write(buffer, offset, count);
                }
            }

            public void Flush()
            {
                if (ssl_choice)
                {
                    this.stream_ssl.Flush();
                }
                else
                {
                    this.stream.Flush();
                }
            }

            public void Close()
            {
                if (ssl_choice)
                {
                    if (stream_ssl != null)
                    {
                        try
                        {
                            this.stream_ssl.Close();
                        }
                        catch { }
                    }
                    this.stream_ssl = null;
                }
                if (this.client != null)
                {
                    try
                    {
                        this.client.Close();
                    }
                    catch { }
                }
                this.client = null;
            }
        }
    }
}