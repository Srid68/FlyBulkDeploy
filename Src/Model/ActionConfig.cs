using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Arshu.FlyDeploy.Model
{
    public class ActionConfig
    {
        #region Property

        public string ApiUser { get; set; }
        public string ApiToken { get; set; }
        public string OrgName { get; set; } = "personal";
        public string AppName { get; set; } = "appweb";
        public int ActionInterval { get; set; } = 1000;

        public List<MachineConfig> MachineConfig { get; set; } = new List<MachineConfig>();
        
        public List<RegionConfig> RegionConfig { get; set; } = new List<RegionConfig>();

        #endregion

        #region Constructor

        [JsonConstructor]
        public ActionConfig(string apiUser, string apiToken, string appName)
        {
            ApiUser = apiUser;
            ApiToken = apiToken;
            AppName = appName;
        }

        #endregion
    }
}
