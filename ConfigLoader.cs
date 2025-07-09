using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BabbleBot
{
    class ConfigLoader
    {

        public static Config? ResolveConfig(string configFile, ILogger logger)
        {
            Config? config;
            if (!File.Exists(configFile))
            {
                // Create default config
                config = new Config();
                // lets assume this doesn't throw. if it does we better crash anyway
                File.WriteAllText(configFile, JsonConvert.SerializeObject(config, Formatting.Indented));

                logger.LogCritical("Config not found! Please assign valid values.");
                return null;
            }

            try
            {
                config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(configFile));
                if (config == null)
                {
                    logger.LogCritical("Config file deserialization error");
                    return null;
                }
                logger.LogInformation("Config file load succesfull");
                return config;
            } catch (Exception ex) {
                logger.LogCritical("Config file deserialization error: {}", ex.Message);
                return null;
            }
        }
    }
}
