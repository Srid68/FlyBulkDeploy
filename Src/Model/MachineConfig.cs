using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Arshu.FlyDeploy.Model
{
    public class MachineConfig
    {
        public string DockerImage { get; set; } = "registry.fly.io/appweb:latest";

        //Action Values are | Stop | Delete | Destroy | Create | Update | CreateOrUpdate
        public string Action { get; set; } = "";
        //Action Type Values are Direct | Rollup Valid for Action Update
        public string ActionType { get; set; } = "";

        public string MachineCreateTemplate { get; set; } = "machine_create.json";
        public string MachineUpdateTemplate { get; set; } = "machine_update.json";

        public List<EnvConfig> EnvConfig { get; set; } = new List<EnvConfig>();
    }
}
