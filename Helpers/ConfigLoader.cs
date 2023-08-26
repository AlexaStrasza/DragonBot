using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DragonBot.Helpers
{
    public static class ConfigLoader
    {
        private const string ConfigFilePath = "configRanks.json"; // Replace with the actual path

        public static ConfigRanks LoadConfig()
        {
            string solutionDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            string databasePath = Path.Combine(solutionDirectory, ConfigFilePath);

            string jsonContent = File.ReadAllText(ConfigFilePath);
            ConfigRanks config = JsonConvert.DeserializeObject<ConfigRanks>(jsonContent);
            return config;
        }
    }
}
