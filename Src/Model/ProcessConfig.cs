using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Arshu.FlyDeploy.Model
{
    public class ProcessConfigInfo
    {
        public string ConfigPath { get; set; } = "deploy.json";
        public bool Process { get; set; } = false;
    }

    public class ProcessConfig
    {
        public List<ProcessConfigInfo> ProcessList { get; set; } = new List<ProcessConfigInfo>();

    }
}
