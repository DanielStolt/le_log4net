using System;
using System.Text;
using System.Configuration;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using log4net;
using log4net.Appender;
using log4net.Layout;
using log4net.Core;
using System.Threading;

/**
 * Tests the Logentries Log4Net Plugin
 * 
 * @author Mark Lacomber
 * 
 */

namespace Log4NetTest
{
    /// <summary>
    /// Summary description for LeAppenderTest
    /// </summary>
    [TestClass]
    public class LeAppenderTest
    {
        /**General LE Appender ready for tweaking */
        LeAppender x;

        /** Some random key */
        readonly static String k0 = Guid.NewGuid().ToString();
        /** Some random key */
        readonly static String k1 = Guid.NewGuid().ToString();
        /** Some random location */
        readonly static String l0 = "location0";
        /** Some random location */
        readonly static String l1 = "location1";

        public LeAppenderTest()
        {
            x = new LeAppender();
        }

        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        [TestMethod]
        public void testLeAppenderBoolean()
        {
            LeAppender l = new LeAppender();
            Assert.IsFalse(l.Debug);
            Assert.IsFalse(l.Ssl);

            l.Close();
        }

        [TestMethod]
        public void testSetKey()
        {
            LeAppender l = new LeAppender();

            l.Key = k0;
            Assert.AreEqual(k0, l.Key);

            l.Key = k1;
            Assert.AreEqual(k1, l.Key);

            l.Close();
        }

        [TestMethod]
        public void testSetLocation()
        {
            LeAppender l = new LeAppender();
            l.Location = l0;
            Assert.AreEqual(l0, l.Location);

            l.Location = l1;
            Assert.AreEqual(l1, l.Location);

            l.Close();
        }

        [TestMethod]
        public void testSetDebug()
        {
            LeAppender l = new LeAppender();
            l.Debug = true;
            Assert.IsTrue(l.Debug);

            l.Debug = false;
            Assert.IsFalse(l.Debug);

            l.Close();
        }

        [TestMethod]
        public void testSetSsl()
        {
            LeAppender l = new LeAppender();
            l.Ssl = true;
            Assert.IsTrue(l.Ssl);

            l.Ssl = false;
            Assert.IsFalse(l.Ssl);

            l.Close();
        }

        [TestMethod]
        public void testCheckCredentials()
        {
            LeAppender l = new LeAppender();
            var appSettings = ConfigurationManager.AppSettings;
            Assert.IsFalse(l.checkCredentials());

            appSettings["LOGENTRIES_ACCOUNT_KEY"] = "";
            Assert.IsFalse(l.checkCredentials());

            appSettings["LOGENTRIES_LOCATION"] = "";
            Assert.IsFalse(l.checkCredentials());

            appSettings["LOGENTRIES_ACCOUNT_KEY"] = k0;
            Assert.IsFalse(l.checkCredentials());

            appSettings["LOGENTRIES_LOCATION"] = l0;
            Assert.IsTrue(l.checkCredentials());

            //Reset appSettings for following tests
            appSettings["LOGENTRIES_ACCOUNT_KEY"] = "";
            appSettings["LOGENTRIES_LOCATION"] = "";

            l.Close();
        }

        [TestMethod]
        public void testAppendLine()
        {
            LeAppender l = new LeAppender();

            String line0 = "line0";
            l.addLine(line0);
            Assert.AreEqual(1, l.queue.Count);

            for (int i = 0; i < LeAppender.QUEUE_SIZE; ++i)
            {
                l.addLine("line" + i);
            }
            Assert.AreEqual(LeAppender.QUEUE_SIZE, l.queue.Count);

            l.Close();
        }

        [TestMethod]
        public void testAppendLoggingEvent()
        {
            LeAppender l = new LeAppender();
            l.Layout = new SimpleLayout();

            LoggingEventData data = new LoggingEventData();
            data.Message = "Critical";
            data.LoggerName = "root";
            data.Level = log4net.Core.Level.Debug;
            data.TimeStamp = DateTime.Now;
            data.ExceptionString = new Exception().ToString();

            LoggingEvent logEvent = new LoggingEvent(data);

            l.TestAppend(logEvent);
            Assert.AreEqual(0, l.queue.Count);

            var appSettings = ConfigurationManager.AppSettings;
            appSettings["LOGENTRIES_ACCOUNT_KEY"] = k0;
            appSettings["LOGENTRIES_LOCATION"] = l0;
            l.TestAppend(logEvent);

            Assert.AreEqual(1, l.queue.Count);

            l.Close();
        }

        [TestMethod]
        public void testCloseAppender()
        {
            LeAppender l = new LeAppender();
            var appSettings = ConfigurationManager.AppSettings;
            appSettings["LOGENTRIES_ACCOUNT_KEY"] = k0;
            appSettings["LOGENTRIES_LOCATION"] = l0;
            l.thread.Start();

            //Wait until thread is active
            for (int i = 0; i < 10; ++i)
            {
                Thread.Sleep(100);
                if (l.thread.IsAlive == true)
                    break;
            }
            Assert.IsTrue(l.thread.IsAlive);

            l.Close();

            //Wait until the thread is not active
            for (int i = 0; i < 10; ++i)
            {
                Thread.Sleep(100);
                if (!l.thread.IsAlive)
                    break;
            }
            Assert.IsFalse(l.thread.IsAlive);
        }
    }
}
