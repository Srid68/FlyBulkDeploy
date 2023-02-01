using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Arshu.FlyDeploy.Model
{
    public class MachineInfo
    {
        public string MachineRegion { get; set; }
        public string MachineName { get; set; }
        public string? MachineID { get; set; }
        public bool Create { get; set; }

        public MachineInfo(string machineRegion, string machineName, string? machineID, bool create =false)
        {
            MachineRegion = machineRegion;
            MachineName = machineName;
            MachineID = machineID;
            Create = create;
        }

        public bool IsValid()
        {
            bool isValid = true;

            if ((Create == false) && (string.IsNullOrEmpty(MachineID) == true))
            {
                isValid = false;
            }

            if ((Create == true) && (string.IsNullOrEmpty(MachineID) == false))
            {
                isValid = false;
            }

            return isValid;
        }
    }    
}
