// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Microsoft.BotBuilderSamples
{
    public class MainDialog : LogoutDialog
    {
        protected readonly ILogger _logger;

        public MainDialog(IConfiguration configuration, ILogger<MainDialog> logger)
            : base(nameof(MainDialog), configuration["ConnectionName"])
        {
            _logger = logger;

            AddDialog(new OAuthPrompt(
                nameof(OAuthPrompt),
                new OAuthPromptSettings
                {
                    ConnectionName = ConnectionName,
                    Text = "Please login",
                    Title = "Login",
                    Timeout = 300000, // User has 5 minutes to login
                }));

            AddDialog(new AppointmentInfoDialog());
            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new ChoicePrompt(nameof(ChoicePrompt)));

            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                PromptStepAsync,
                LoginStepAsync,
                CommandStepAsync,
                ProcessStepAsync
            }));

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> PromptStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            
            return await stepContext.BeginDialogAsync(nameof(OAuthPrompt), null, cancellationToken);
            
        }

        private async Task<DialogTurnResult> LoginStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // Get the token from the previous step. Note that we could also have gotten the
            // token directly from the prompt itself. There is an example of this in the next method.
            var tokenResponse = (TokenResponse)stepContext.Result;
          
            if (tokenResponse != null)
            {
                await OAuthHelpers.ListMeAsync(stepContext.Context, tokenResponse);

                //await stepContext.Context.SendActivityAsync(MessageFactory.Text("You are now logged in."), cancellationToken);
                return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = MessageFactory.Text("Would you like to view or create? (type 'mycalendar', or 'groupcalendar', or 'setappointment',or 'create group calendar')") }, cancellationToken);

            }

            await stepContext.Context.SendActivityAsync(MessageFactory.Text("Login was not successful please try again."), cancellationToken);
            return await stepContext.EndDialogAsync();
        }

        private async Task<DialogTurnResult> CommandStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            stepContext.Values["command"] = stepContext.Result;

            // Call the prompt again because we need the token. The reasons for this are:
            // 1. If the user is already logged in we do not need to store the token locally in the bot and worry
            // about refreshing it. We can always just call the prompt again to get the token.
            // 2. We never know how long it will take a user to respond. By the time the
            // user responds the token may have expired. The user would then be prompted to login again.
            //
            // There is no reason to store the token locally in the bot because we can always just call
            // the OAuth prompt to get the token or get a new token if needed.
            return await stepContext.BeginDialogAsync(nameof(OAuthPrompt), null, cancellationToken);
        }

        private async Task<DialogTurnResult> ProcessStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (stepContext.Result != null)
            {
                // We do not need to store the token in the bot. When we need the token we can
                // send another prompt. If the token is valid the user will not need to log back in.
                // The token will be available in the Result property of the task.
                var tokenResponse = stepContext.Result as TokenResponse;

                // If we have the token use the user is authenticated so we may use it to make API calls.
                if (tokenResponse?.Token != null)
                {
                    var command = ((string)stepContext.Values["command"] ?? string.Empty).ToLowerInvariant();
                    var events = await SimpleGraphClient.GetAuthenticatedClient(tokenResponse.Token).GetCalendarEvents();
                    var groupevents = await SimpleGraphClient.GetAuthenticatedClient(tokenResponse.Token).GetGroupCalendarEvents();
                    
                    if (command == "mycalendar")
                    {
                        
                        string eventsFormatted = string.Join("\n",
                        events.Select(s => $"- {s.Subject} from {DateTime.Parse(s.Start.DateTime).ToShortTimeString()} till {DateTime.Parse(s.End.DateTime).ToShortTimeString()}")
                            .ToList());

                        await stepContext.Context.SendActivityAsync("You have the following events: \n" + eventsFormatted,
                            cancellationToken: cancellationToken);
                        

                       


                    }
                    else if (command.StartsWith("groupcalendar"))
                    {
                        
                        string groupeventsFormatted = string.Join("\n",
                        groupevents.Select(s => $"- {s.Subject} from {DateTime.Parse(s.Start.DateTime).ToShortTimeString()} till {DateTime.Parse(s.End.DateTime).ToShortTimeString()}")
                            .ToList());

                        await stepContext.Context.SendActivityAsync("Your group have the following events: \n" + groupeventsFormatted,
                            cancellationToken: cancellationToken);
                    }

                    else if (command.StartsWith("setappointment"))
                    {
                        return await stepContext.BeginDialogAsync(nameof(AppointmentInfoDialog), tokenResponse, cancellationToken);
                    }
                    else if (command.StartsWith("create group calendar"))
                    {
                        
                        await SimpleGraphClient.GetAuthenticatedClient(tokenResponse.Token).CreateGroupCalendar();

                        await stepContext.Context.SendActivityAsync("DEMO-GROUP-CALENDAR created \n" ,
                            cancellationToken: cancellationToken);
                    }
                    else
                    {
                        // await stepContext.Context.SendActivityAsync(MessageFactory.Text($"Your token is: {tokenResponse.Token}"), cancellationToken);
                        await stepContext.Context.SendActivityAsync(MessageFactory.Text($"Oops! Thats not a valid entry"), cancellationToken);

                    }
                }
            }
            else
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("We couldn't log you in. Please try again later."), cancellationToken);
            }

            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }
    }
}
