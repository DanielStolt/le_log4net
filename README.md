Logging To Logentries from AppHarbor using Log4net
========================================================

Simple Usage Example
----------------------


    public class HomeController : Controller
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(HomeController).Name);
        
        public ActionResult Index()
        {
            log.Debug("Home page opened");
            
            ViewBag.Message = "Welcome to ASP.NET MVC!";
            
            log.Warn("This is a warning message");
            
            return View();
        }

        public ActionResult About()
        {
            return View();
        }
    }
    
------------------------

To configure Log4Net, you will need to perform the following:

    * (1) Obtain your Logentries account key.
    * (2) Setup Log4Net (if you are not already using it).
    * (3) Configure the Logentries Log4Net plugin.

You can obtain your Logentries account key on the Logentries UI, by clicking account in the top left corner

and then display account key on the right.

Logentries Log4net Plugin Setup
--------------------------------

To install the Logentries Plugin Library, we suggest using Nuget.

The package is found at https://nuget.org/List/Packages/le_log4net

This will also install Log4Net into your project if it is not already installed.

If you're not using Nuget, the library can be downloaded from:

https://github.com/downloads/MarkLC/le_log4net_async/LeLog4net.dll

It will need to be referenced in your project.

If using this option, please make sure to install Log4Net appropriately. 

LoggerConf
------------------

The following configuration needs to be placed in your web.config file directly underneath

the opening  `<configuration>`
 
    <configSections>
        <section name="log4net" type="log4net.Config.Log4NetConfigurationSectionHandler, log4net" />
    </configSections>
    <appSettings>
      <add key="LOGENTRIES_ACCOUNT_KEY" value="" />
      <add key="LOGENTRIES_LOCATION" value="" />
    </appSettings>
    <log4net>
      <appender name="LeAppender" type="log4net.Appender.LeAppender, LeLog4net">
        <Key value="LOGENTRIES_ACCOUNT_KEY" />
        <Location value="LOGENTRIES_LOCATION" />
        <Debug value="true" />
	  <Ssl value="false" />
        <layout type="log4net.Layout.PatternLayout">
          <param name="ConversionPattern" value="%d{ddd MMM dd HH:mm:ss zzz yyyy} %logger %: %level%, %m" />
        </layout>
      </appender>
      <root>
        <level value="ALL" />
        <appender-ref ref="LeAppender" />
      </root>
    </log4net>

In the appSettings subsection, using your account-key which you obtained earlier, fill in the value for LOGENTRIES_ACCOUNT_KEY. Also replace the "LOGENTRIES_LOCATION" value with the location of your logfile on Logentries. This should be in the following format:

    hostname/logname.log    

If you would rather create a host and log from your command line instead of the Logentries UI,

you can use the following program: 

https://github.com/downloads/logentries/le_log4net/getkey.zip

Run it as follows:   getKey.exe --register

Finally place the following line in your `AssemblyInfo.cs` file as is required to use log4net:

    [assembly: log4net.Config.XmlConfigurator(ConfigFile="Web.config",Watch=true)]


Logging Messages
----------------

With that done, you are ready to send logs to Logentries.

In each class you wish to log from, enter the following using directives at the top if not already there:

    using log4net;

Then create this object at class-level:

    private static readonly ILog log = LogManager.GetLogger(typeof(your_class_name_here).Name);

Be sure to enter the name of current class in the indicated brackets above.

What this does is create a logger with the name of the current class for
clarity in the logs.

Now within your code in that class, you can log using log4net as normal and it
will log to Logentries.

Example:

    log.Debug("Debugging Message");
    log.Info("Informational message");
    log.Warn("Warning Message");
