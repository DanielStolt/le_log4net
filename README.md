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


To configure Log4Net, you will need to perform the following:

    * (1) Create a Logentries Account.
    * (2) Setup Log4Net (if you are not already using it).
    * (3) Configure the Logentries Log4Net plugin.


Create your Logentries Account
------------------------------
You can register your account on Logentries simply by clicking `Sign Up` at the top of the screen.
Once logged in, create a new host with a name that best represents your app. Select this host and create a 
new logfile of source type `TOKEN TCP` with a name that represents what you will be logging, these names are for your own benefit.

Logentries Log4net Plugin Setup
--------------------------------

To install the Logentries Plugin Library, we suggest using Nuget.

The package is found at <https://nuget.org/List/Packages/le_log4net>

This will also install Log4Net into your project if it is not already installed.

If you wish to install it manually, you can find LeLog4net.dll in the downloads tab for this repo.

If using this option, please make sure to install Log4Net appropriately. 

Log4Net Config
---------------

In the `<appSettings>` section of your `Web/App.config`, replace `LOGENTRIES_TOKEN` with the 
token that is printed in grey beside the logfile you created in the Logentries UI.

To configure Log4Net along with the plug-in, paste the following into your `Web/App.config` directly underneath the opening
`<configuration>`

    <configSections>
        <section name="log4net" type="log4net.Config.Log4NetConfigurationSectionHandler, log4net" />
    </configSections>
    <log4net>
      <appender name="LeAppender" type="log4net.Appender.LogentriesAppender, LeLog4net">
        <Debug value="true" />
        <HttpPut value="false" />
        <layout type="log4net.Layout.PatternLayout">
          <param name="ConversionPattern" value="%d{ddd MMM dd HH:mm:ss zzz yyyy} %logger %: %level%, %m, " />
        </layout>
      </appender>
      <root>
        <level value="ALL" />
        <appender-ref ref="LeAppender" />
      </root>
    </log4net>

If you are using App.config in your project, you will need to set the "Copy to
output Directory" property of App.config to "Copy always". This can be done
inside Visual Studio. 
    
AssemblyInfo.cs
-------------------

Finally place the following line in your `AssemblyInfo.cs` file as log4net needs to be explicitly told what the config file is called:

For Web apps:

    [assembly: log4net.Config.XmlConfigurator(ConfigFile="Web.config",Watch=true)]

For Console apps:

    [assembly: log4net.Config.XmlConfigurator(ConfigFile="App.config",Watch=true)]

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

Now within your code in that class, you can log using log4net as normal and it will log to Logentries.

Example:

    log.Debug("Debugging Message");
    log.Info("Informational message");
    log.Warn("Warning Message");

Troubleshooting
----------------

The Logentries Plugin logs its debug messages to log4net's internal logger. This is enabled in your `web/app.config` by default and can be disabled by changing the `log4net.Internal.Debug` in the `<appSettings>` section to false. If you would like to keep log4net debug enabled, but disable Logentries debug messages, then change the debug parameter inside the `<log4net>` section to false.

You can also download a hello world sample app from the Downloads section. It is ready to go and only needs `LOGENTRIES_TOKEN` to be entered into the `Web/App.config`.

Ensure that you followed the section of this readme regarding your AssemblyInfo.cs file.
