using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nuke.Common.CI.AzurePipelines;

namespace NukeExtensions
{
    public static class AzurePipelinesExtensions
    {
        public static void SetVariable(this AzurePipelines instance, string variableName, string variableValue, bool isOutput = true)
        {
            instance?.WriteCommand($"task.setvariable variable={variableName};isOutput={isOutput}", variableValue);
        }
    }
}
