using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Arshu.FlyDeploy.Model
{
    public class EnvConfig
    {
        public string EnvName { get; set; } = "";
        public string EnvValue { get; set; } = "";

        [JsonConstructor]
        public EnvConfig()
        {

        }
    }
}
