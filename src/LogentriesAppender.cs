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
 *   VERSION:  2.3.7
 */

using System;
using System.Collections.Concurrent;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using log4net.Appender;
using log4net.Core;
using log4net.Util;

namespace log4net.Appender
{
    public class LogentriesAppender : AppenderSkeleton
    {
        #region Constants

        // Current version number.
        protected const String Version = "2.3.7";

        // Size of the internal event queue. 
        protected const int QueueSize = 32768;

        // Logentries API server address. 
        protected const String LeApiUrl = "api.logentries.com";

        // Port number for token logging on Logentries API server. 
        protected const int LeApiTokenPort = 10000;

        // Port number for TLS encrypted token logging on Logentries API server 
        protected const int LeApiTokenTlsPort = 20000;

        // Port number for HTTP PUT logging on Logentries API server. 
        protected const int LeApiHttpPort = 80;

        // Port number for SSL HTTP PUT logging on Logentries API server. 
        protected const int LeApiHttpsPort = 443;

        // Minimal delay between attempts to reconnect in milliseconds. 
        protected const int MinDelay = 100;

        // Maximal delay between attempts to reconnect in milliseconds. 
        protected const int MaxDelay = 10000;

        // Appender signature - used for debugging messages. 
        protected const String LeSignature = "LE: ";

        // Logentries configuration names. 
        protected const String ConfigTokenName = "LOGENTRIES_TOKEN";
        protected const String ConfigAccountKeyName = "LOGENTRIES_ACCOUNT_KEY";
        protected const String ConfigLocationName = "LOGENTRIES_LOCATION";

        // Error message displayed when invalid token is detected. 
        protected const String InvalidTokenMessage = "\n\nIt appears your LOGENTRIES_TOKEN value is invalid or missing.\n\n";

        // Error message displayed when invalid account_key or location parameters are detected. 
        protected const String InvalidHttpPutCredentialsMessage = "\n\nIt appears your LOGENTRIES_ACCOUNT_KEY or LOGENTRIES_LOCATION values are invalid or missing.\n\n";

        // Error message deisplayed when queue overflow occurs. 
        protected const String QueueOverflowMessage = "\n\nLogentries buffer queue overflow. Message dropped.\n\n";

        #endregion

        #region Singletons

        // UTF-8 output character set. 
        protected static readonly UTF8Encoding UTF8 = new UTF8Encoding();

        // ASCII character set used by HTTP. 
        protected static readonly ASCIIEncoding ASCII = new ASCIIEncoding();

        // Logentries API server certificate. 
        protected static readonly X509Certificate2 LeApiServerCertificate =
            new X509Certificate2(Encoding.UTF8.GetBytes(
@"-----BEGIN CERTIFICATE-----
MIIFSjCCBDKgAwIBAgIDBQMSMA0GCSqGSIb3DQEBBQUAMGExCzAJBgNVBAYTAlVT
MRYwFAYDVQQKEw1HZW9UcnVzdCBJbmMuMR0wGwYDVQQLExREb21haW4gVmFsaWRh
dGVkIFNTTDEbMBkGA1UEAxMSR2VvVHJ1c3QgRFYgU1NMIENBMB4XDTEyMDkxMDE5
NTI1N1oXDTE2MDkxMTIxMjgyOFowgcExKTAnBgNVBAUTIEpxd2ViV3RxdzZNblVM
ek1pSzNiL21hdktiWjd4bEdjMRMwEQYDVQQLEwpHVDAzOTM4NjcwMTEwLwYDVQQL
EyhTZWUgd3d3Lmdlb3RydXN0LmNvbS9yZXNvdXJjZXMvY3BzIChjKTEyMS8wLQYD
VQQLEyZEb21haW4gQ29udHJvbCBWYWxpZGF0ZWQgLSBRdWlja1NTTChSKTEbMBkG
A1UEAxMSYXBpLmxvZ2VudHJpZXMuY29tMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8A
MIIBCgKCAQEAxcmFqgE2p6+N9lM2GJhe8bNUO0qmcw8oHUVrsneeVA66hj+qKPoJ
AhGKxC0K9JFMyIzgPu6FvuVLahFZwv2wkbjXKZLIOAC4o6tuVb4oOOUBrmpvzGtL
kKVN+sip1U7tlInGjtCfTMWNiwC4G9+GvJ7xORgDpaAZJUmK+4pAfG8j6raWgPGl
JXo2hRtOUwmBBkCPqCZQ1mRETDT6tBuSAoLE1UMlxWvMtXCUzeV78H+2YrIDxn/W
xd+eEvGTSXRb/Q2YQBMqv8QpAlarcda3WMWj8pkS38awyBM47GddwVYBn5ZLEu/P
DiRQGSmLQyFuk5GUdApSyFETPL6p9MfV4wIDAQABo4IBqDCCAaQwHwYDVR0jBBgw
FoAUjPTZkwpHvACgSs5LdW6gtrCyfvwwDgYDVR0PAQH/BAQDAgWgMB0GA1UdJQQW
MBQGCCsGAQUFBwMBBggrBgEFBQcDAjAdBgNVHREEFjAUghJhcGkubG9nZW50cmll
cy5jb20wQQYDVR0fBDowODA2oDSgMoYwaHR0cDovL2d0c3NsZHYtY3JsLmdlb3Ry
dXN0LmNvbS9jcmxzL2d0c3NsZHYuY3JsMB0GA1UdDgQWBBRaMeKDGSFaz8Kvj+To
j7eMOtT/zTAMBgNVHRMBAf8EAjAAMHUGCCsGAQUFBwEBBGkwZzAsBggrBgEFBQcw
AYYgaHR0cDovL2d0c3NsZHYtb2NzcC5nZW90cnVzdC5jb20wNwYIKwYBBQUHMAKG
K2h0dHA6Ly9ndHNzbGR2LWFpYS5nZW90cnVzdC5jb20vZ3Rzc2xkdi5jcnQwTAYD
VR0gBEUwQzBBBgpghkgBhvhFAQc2MDMwMQYIKwYBBQUHAgEWJWh0dHA6Ly93d3cu
Z2VvdHJ1c3QuY29tL3Jlc291cmNlcy9jcHMwDQYJKoZIhvcNAQEFBQADggEBAAo0
rOkIeIDrhDYN8o95+6Y0QhVCbcP2GcoeTWu+ejC6I9gVzPFcwdY6Dj+T8q9I1WeS
VeVMNtwJt26XXGAk1UY9QOklTH3koA99oNY3ARcpqG/QwYcwaLbFrB1/JkCGcK1+
Ag3GE3dIzAGfRXq8fC9SrKia+PCdDgNIAFqe+kpa685voTTJ9xXvNh7oDoVM2aip
v1xy+6OfZyGudXhXag82LOfiUgU7hp+RfyUG2KXhIRzhMtDOHpyBjGnVLB0bGYcC
566Nbe7Alh38TT7upl/O5lA29EoSkngtUWhUnzyqYmEMpay8yZIV4R9AuUk2Y4HB
kAuBvDPPm+C0/M4RLYs=
-----END CERTIFICATE-----"));

        // Newline char to trim from message for formatting. 
        protected static char[] TrimChars = { '\n' };

        //static list of all the queues the le appender might be managing.
        private static ConcurrentBag<BlockingCollection<string>> _allQueues = new ConcurrentBag<BlockingCollection<string>>(); 

        /// <summary>
        /// Determines if the queue is empty after waiting the specified waitTime.
        /// Returns true or false if the underlying queues are empty.
        /// </summary>
        /// <param name="waitTime">The length of time the method should block before giving up waiting for it to empty.</param>
        /// <returns>True if the queue is empty, false if there are still items waiting to be written.</returns>
        public static bool AreAllQueuesEmpty(TimeSpan waitTime)
        {
            var start = DateTime.UtcNow;
            var then = DateTime.UtcNow;

            while (start.Add(waitTime) > then)
            {
                if (_allQueues.All(x => x.Count == 0))
                    return true;

                Thread.Sleep(100);
                then = DateTime.UtcNow;
            }

            return _allQueues.All(x => x.Count == 0);
        }
        #endregion

        public LogentriesAppender()
        {
            Queue = new BlockingCollection<string>(QueueSize);
            _allQueues.Add(Queue);

            WorkerThread = new Thread(new ThreadStart(Run));
            WorkerThread.Name = "Logentries Log4net Appender";
            WorkerThread.IsBackground = true;
        }

        protected readonly BlockingCollection<string> Queue;
        protected readonly Thread WorkerThread;
        protected readonly Random Random = new Random();

        protected LogentriesTcpClient TcpClient = null;
        protected bool IsRunning = false;

        #region Configuration properties

        private String m_Token = "";
        private String m_AccountKey = "";
        private String m_Location = "";
        private bool m_ImmediateFlush = false;
        private bool m_Debug = false;
        private bool m_UseHttpPut = false;
        private bool m_UseSsl = false;

        /* Option to set LOGENTRIES_TOKEN programmatically or in appender definition. */
        public string Token
        {
            get
            {
                return m_Token;
            }
            set
            {
                m_Token = value;
            }
        }

        /* Option to set LOGENTRIES_ACCOUNT_KEY programmatically or in appender definition. */
        public String AccountKey
        {
            get
            {
                return m_AccountKey;
            }
            set
            {
                m_AccountKey = value;
            }
        }

        /* Option to set LOGENTRIES_LOCATION programmatically or in appender definition. */
        public String Location
        {
            get
            {
                return m_Location;
            }
            set
            {
                m_Location = value;
            }
        }

        /* Set to true to always flush the TCP stream after every written entry. */
        public bool ImmediateFlush
        {
            get
            {
                return m_ImmediateFlush;
            }
            set
            {
                m_ImmediateFlush = value;
            }
        }

        /* Debug flag. */
        public bool Debug
        {
            get
            {
                return m_Debug;
            }
            set
            {
                m_Debug = value;
            }
        }


        /* Set to true to use HTTP PUT logging. */
        public bool UseHttpPut
        {
            get
            {
                return m_UseHttpPut;
            }
            set
            {
                m_UseHttpPut = value;
            }
        }

        /* This property exists for backward compatibility with older configuration XML. */
        [Obsolete("Use the UseHttpPut property instead.")]
        public bool HttpPut
        {
            get
            {
                return m_UseHttpPut;
            }
            set
            {
                m_UseHttpPut = value;
            }
        }


        /* Set to true to use SSL with HTTP PUT logging. */
        public bool UseSsl
        {
            get
            {
                return m_UseSsl;
            }
            set
            {
                m_UseSsl = value;
            }
        }

        /* This property exists for backward compatibility with older configuration XML. */
        [Obsolete("Use the UseHttpPut property instead.")]
        public bool Ssl
        {
            get
            {
                return m_UseSsl;
            }
            set
            {
                m_UseSsl = value;
            }
        }

        #endregion

        #region AppenderSkeleton overrides

        public void TestAppend(LoggingEvent logEvent)
        {
            // Used for unit testing since the Append method is protected.
            this.Append(logEvent);
        }

        protected override void Append(LoggingEvent loggingEvent)
        {
            if (!IsRunning)
            {
                if (LoadCredentials())
                {
                    WriteDebugMessages("Starting Logentries asynchronous socket client.");
                    WorkerThread.Start();
                    IsRunning = true;
                }
            }

            var renderedEvent = RenderLoggingEvent(loggingEvent).TrimEnd(TrimChars);
            AddLine(renderedEvent);
        }

        protected override void Append(LoggingEvent[] loggingEvents)
        {
            foreach (var logEvent in loggingEvents)
            {
                this.Append(logEvent);
            }
        }

        protected override bool RequiresLayout
        {
            get
            {
                return true;
            }
        }

        protected override void OnClose()
        {
            WorkerThread.Interrupt();
        }

        #endregion

        #region Protected methods

        protected virtual void Run()
        {
            try
            {
                // Open connection.
                ReopenConnection();

                // Send data in queue.
                while (true)
                {
                    // Take data from queue.
                    var line = Queue.Take();

                    // Replace newline chars with line separator to format multi-line events nicely.
                    line = line.Replace(Environment.NewLine, "\u2028");
                    string finalLine = (!UseHttpPut ? this.Token + line : line) + '\n';
                    byte[] data = UTF8.GetBytes(finalLine);

                    // Send data, reconnect if needed.
                    while (true)
                    {
                        try
                        {
                            this.TcpClient.Write(data, 0, data.Length);

                            if (m_ImmediateFlush)
                                this.TcpClient.Flush();
                        }
                        catch (IOException)
                        {
                            // Reopen the lost connection.
                            ReopenConnection();
                            continue;
                        }

                        break;
                    }
                }
            }
            catch (ThreadInterruptedException ex)
            {
                WriteDebugMessages("Logentries asynchronous socket client was interrupted.", ex);
            }
        }

        protected virtual void OpenConnection()
        {
            try
            {
                if (TcpClient == null)
                    TcpClient = new LogentriesTcpClient(UseHttpPut, UseSsl);

                TcpClient.Connect();

                if (UseHttpPut)
                {
                    var header = String.Format("PUT /{0}/hosts/{1}/?realtime=1 HTTP/1.1\r\n\r\n", m_AccountKey, m_Location);
                    TcpClient.Write(ASCII.GetBytes(header), 0, header.Length);
                }
            }
            catch (Exception ex)
            {
                throw new IOException("An error occurred while opening the connection.", ex);
            }
        }

        protected virtual void ReopenConnection()
        {
            CloseConnection();

            var rootDelay = MinDelay;
            while (true)
            {
                try
                {
                    OpenConnection();

                    return;
                }
                catch (Exception ex)
                {
                    if (Debug)
                    {
                        WriteDebugMessages("Unable to connect to Logentries API.", ex);
                    }
                }

                rootDelay *= 2;
                if (rootDelay > MaxDelay)
                    rootDelay = MaxDelay;

                var waitFor = rootDelay + Random.Next(rootDelay);

                try
                {
                    Thread.Sleep(waitFor);
                }
                catch
                {
                    throw new ThreadInterruptedException();
                }
            }
        }

        protected virtual void CloseConnection()
        {
            if (TcpClient != null)
                TcpClient.Close();
        }

        protected virtual void AddLine(string line)
        {
            WriteDebugMessages("Queueing: " + line);

            // Try to append data to queue.
            if (!Queue.TryAdd(line))
            {
                Queue.Take();
                if (!Queue.TryAdd(line))
                    WriteDebugMessages(QueueOverflowMessage);
            }
        }

        protected virtual bool LoadCredentials()
        {
            var appSettings = ConfigurationManager.AppSettings;

            if (!UseHttpPut)
            {
                if (GetIsValidGuid(Token))
                    return true;

                var configToken = appSettings[ConfigTokenName];
                if (!String.IsNullOrEmpty(configToken) && GetIsValidGuid(configToken))
                {
                    Token = configToken;
                    return true;
                }

                WriteDebugMessages(InvalidTokenMessage);
                return false;
            }

            if (AccountKey != "" && GetIsValidGuid(AccountKey) && Location != "")
                return true;

            var configAccountKey = appSettings[ConfigAccountKeyName];
            if (!String.IsNullOrEmpty(configAccountKey) && GetIsValidGuid(configAccountKey))
            {
                AccountKey = configAccountKey;

                var configLocation = appSettings[ConfigLocationName];
                if (!String.IsNullOrEmpty(configLocation))
                {
                    Location = configLocation;
                    return true;
                }
            }

            WriteDebugMessages(InvalidHttpPutCredentialsMessage);
            return false;
        }

        protected virtual bool GetIsValidGuid(string guidString)
        {
            if (String.IsNullOrEmpty(guidString))
                return false;

            Guid parsedGuid;
            return Guid.TryParse(guidString, out parsedGuid);
        }

        protected virtual void WriteDebugMessages(string message, Exception ex)
        {
            if (!Debug)
                return;

            message = LeSignature + message;
            string[] messages = { message, ex.ToString() };
            foreach (var msg in messages)
            {
                // Use below line instead when compiling with log4net1.2.10.
                //LogLog.Debug(msg);

                LogLog.Debug(typeof(LogentriesAppender), msg);
            }
        }

        protected virtual void WriteDebugMessages(string message)
        {
            if (!Debug)
                return;

            message = LeSignature + message;

            // Use below line instead when compiling with log4net1.2.10.
            //LogLog.Debug(message);

            LogLog.Debug(typeof(LogentriesAppender), message);
        }

        protected virtual string SubstituteAppSetting(string key)
        {
            var appSettings = ConfigurationManager.AppSettings;
            if (appSettings.HasKeys() && appSettings.AllKeys.Contains(key))
                return appSettings[key];
            else
                return key;
        }

        #endregion

        /// <summary>
        /// Custom class to support both HTTP PUT and Token-based logging as well as TLS/SSL.
        /// </summary>
        protected class LogentriesTcpClient
        {
            public LogentriesTcpClient(bool useHttpPut, bool useSsl)
            {
                m_UseSsl = useSsl;
                if (!m_UseSsl)
                    m_TcpPort = useHttpPut ? LeApiHttpPort : LeApiTokenPort;
                else
                    m_TcpPort = useHttpPut ? LeApiHttpsPort : LeApiTokenTlsPort;
            }

            private bool m_UseSsl = false;
            private int m_TcpPort;
            private TcpClient m_Client = null;
            private Stream m_Stream = null;
            private SslStream m_SslStream = null;

            private Stream ActiveStream
            {
                get
                {
                    return m_UseSsl ? m_SslStream : m_Stream;
                }
            }

            public void Connect()
            {
                m_Client = new TcpClient(LeApiUrl, m_TcpPort);
                m_Client.NoDelay = true;

                m_Stream = m_Client.GetStream();

                if (m_UseSsl)
                {
                    m_SslStream = new SslStream(m_Stream, false, (sender, cert, chain, errors) => cert.GetCertHashString() == LeApiServerCertificate.GetCertHashString());
                    m_SslStream.AuthenticateAsClient(LeApiUrl);
                }
            }

            public void Write(byte[] buffer, int offset, int count)
            {
                ActiveStream.Write(buffer, offset, count);
            }

            public void Flush()
            {
                ActiveStream.Flush();
            }

            public void Close()
            {
                if (m_Client != null)
                {
                    try
                    {
                        m_Client.Close();
                    }
                    catch
                    {
                    }
                }
            }
        }
    }
}
