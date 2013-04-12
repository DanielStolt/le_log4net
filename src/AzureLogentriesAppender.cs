using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace MiP.Cms.Providers.Logging
{
    public class AzureLogentriesAppender : BetterLogentriesAppender
    {
        protected override bool LoadCredentials()
        {
            if (!UseHttpPut)
            {
                if (GetIsValidGuid(Token))
                    return true;

                var configToken = RoleEnvironment.GetConfigurationSettingValue(ConfigTokenName);
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

            var configAccountKey = RoleEnvironment.GetConfigurationSettingValue(ConfigAccountKeyName);
            if (!String.IsNullOrEmpty(configAccountKey) && GetIsValidGuid(configAccountKey))
            {
                AccountKey = configAccountKey;

                var configLocation = RoleEnvironment.GetConfigurationSettingValue(ConfigLocationName);
                if (!String.IsNullOrEmpty(configLocation))
                {
                    Location = configLocation;
                    return true;
                }
            }

            WriteDebugMessages(InvalidHttpPutCredentialsMessage);
            return false;
        }
    }
}
