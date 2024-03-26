using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nuke.Common.CI.AzurePipelines;
using Nuke.Common.Utilities.Collections;

namespace NukeExtensions
{
    public static class AzurePipelinesExtensions
    {
        public static void SetOutputVariable(this AzurePipelines instance, string variableName, string variableValue)
        {
            instance?.WriteCommand($"task.setvariable variable={variableName};isOutput=true", variableValue);
        }
    }
}
