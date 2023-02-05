using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Arshu.FlyDeploy.Model
{
    public class RegionConfig
    {
        public string RegionCode { get; set; } = "sin";
        public bool Process { get; set; } = false;

        [JsonConstructor]
        public RegionConfig(string regionCode, bool process)
        {
            RegionCode = regionCode;
            Process = process;
        }
    }
}
