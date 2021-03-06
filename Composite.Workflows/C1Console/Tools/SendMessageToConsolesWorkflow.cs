using System;
using System.Workflow.Runtime;
using Composite.C1Console.Actions;
using Composite.C1Console.Events;
using Composite.C1Console.Workflow;


namespace Composite.C1Console.Tools
{
    public sealed partial class SendMessageToConsolesWorkflow : Composite.C1Console.Workflow.Activities.FormsWorkflow
    {
        public SendMessageToConsolesWorkflow()
        {
            InitializeComponent();
        }



        private void initializeCodeActivity_InitializeBindings_ExecuteCode(object sender, EventArgs e)
        {
            this.UpdateBinding("Title", "");
            this.UpdateBinding("Message", "");
        }



        private void sendMessageCodeActivity_SendMessage_ExecuteCode(object sender, EventArgs e)
        {
            this.CloseCurrentView();

            string title = this.GetBinding<string>("Title");
            string message = this.GetBinding<string>("Message");
            
            FlowControllerServicesContainer flowControllerServicesContainer = WorkflowFacade.GetFlowControllerServicesContainer(WorkflowEnvironment.WorkflowInstanceId);
            IManagementConsoleMessageService managementConsoleMessageService = flowControllerServicesContainer.GetService<IManagementConsoleMessageService>();
            managementConsoleMessageService.ShowGlobalMessage(DialogType.Message, title, message);
        }
    }
}
