using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Schema;
using Microsoft.Graph;

namespace Microsoft.BotBuilderSamples
{
    public class AppointmentInfoDialog : ComponentDialog
    {
        public AppointmentInfoDialog()
        {
            AddDialog(new TextPrompt(nameof(TextPrompt)));

            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
                {
                    TitleStepAsync,
                    StartTimeSelectionStepAsync,
                    EndTimeSelectionStepAsync,
                    CreateAppointmentStepAsync,
                }));

            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> TitleStepAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken)
        {
            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = MessageFactory.Text("Meeting Title") }, cancellationToken);
        }

        private async Task<DialogTurnResult> StartTimeSelectionStepAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken)
        {
            stepContext.Values["title"] = (string)stepContext.Result;

            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = MessageFactory.Text("Start time") }, cancellationToken);
        }


        private async Task<DialogTurnResult> EndTimeSelectionStepAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken)
        {
            stepContext.Values["startTime"] = (string)stepContext.Result;

            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = MessageFactory.Text("End time") }, cancellationToken);
        }


        private async Task<DialogTurnResult> CreateAppointmentStepAsync(
            WaterfallStepContext stepContext,
            CancellationToken cancellationToken)
        {
            stepContext.Values["endTime"] = (string)stepContext.Result;

            var tokenResponse = stepContext.Options as TokenResponse;
            var start = new DateTimeTimeZone
            {
                DateTime = (string)stepContext.Values["startTime"],
                TimeZone = "Pacific Standard Time"
            };

            var end = new DateTimeTimeZone
            {
                DateTime = (string)stepContext.Values["endTime"],
                TimeZone = "Pacific Standard Time"
            };

            var title = (string)stepContext.Values["title"];
            await SimpleGraphClient.GetAuthenticatedClient(tokenResponse.Token).SetAppointment(title, start, end);
            await stepContext.Context.SendActivityAsync("Event created",
                cancellationToken: cancellationToken);
            return await stepContext.EndDialogAsync();
        }
    }
}
