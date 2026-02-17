﻿﻿﻿using Chat2Report.Models;
using Chat2Report.Providers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Chat2Report.Agents.WorkflowSteps
{

    /// <summary>
    /// Основен, не-генерички интерфејс за workflow чекор.
    /// Сите чекори го имплементираат овој интерфејс, директно или индиректно.
    /// Овозможува униформно креирање и ракување со чекори во фабриката и DI контејнерот.
    /// Чекорите кои работат директно со `WorkflowState` го имплементираат овој интерфејс.
    /// </summary>
    public interface IBaseWorkflowStep
    {
    }


    public interface IConfigurableStep
    {
        void Configure(JsonElement config);
    }



}
