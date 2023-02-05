using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Arshu.FlyDeploy.Model
{
    public class ProcessConfigInfo
    {
        public string ConfigPath { get; set; } = "deploy.json";
        public bool Process { get; set; } = false;

        [JsonConstructor]
        public ProcessConfigInfo(string configPath, bool process)
        {
            ConfigPath = configPath;
            Process = process;
        }

        public ProcessConfigInfo() { }
    }

    public class ProcessConfig
    {
        public List<ProcessConfigInfo> ProcessList { get; set; } = new List<ProcessConfigInfo>();
        public bool Process { get; set; } = false;

        [JsonConstructor]
        public ProcessConfig(bool process)
        {
            Process = process;
        }

        public ProcessConfig() { }
    }
}
