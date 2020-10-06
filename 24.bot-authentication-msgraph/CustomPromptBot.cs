using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Recognizers.Text;
using Microsoft.Recognizers.Text.DateTime;
using Microsoft.Recognizers.Text.Number;

namespace BotAuthenticationMSGraph
{
    public class CustomPromptBot : ActivityHandler
    {
        private readonly BotState _userState;
        private readonly BotState _conversationState;

        public CustomPromptBot(ConversationState conversationState, UserState userState)
        {
            _conversationState = conversationState;
            _userState = userState;
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {

            var conversationStateAccessors = _conversationState.CreateProperty<ConversationFlow>(nameof(ConversationFlow));
            var flow = await conversationStateAccessors.GetAsync(turnContext, () => new ConversationFlow(), cancellationToken);

            var userStateAccessors = _userState.CreateProperty<UserProfile>(nameof(UserProfile));
            var profile = await userStateAccessors.GetAsync(turnContext, () => new UserProfile(), cancellationToken);

            await FillOutUserProfileAsync(flow, profile, turnContext, cancellationToken);

            // Save changes.
            await _conversationState.SaveChangesAsync(turnContext, false, cancellationToken);
            await _userState.SaveChangesAsync(turnContext, false, cancellationToken);
        }

        private static async Task FillOutUserProfileAsync(ConversationFlow flow, UserProfile profile, ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var input = turnContext.Activity.Text?.Trim();
            string message;

            switch (flow.LastQuestionAsked)
            {
                case ConversationFlow.Question.None:
                    await turnContext.SendActivityAsync("Please enter event subject", null, null, cancellationToken);
                    flow.LastQuestionAsked = ConversationFlow.Question.Subject;
                    break;
                case ConversationFlow.Question.Subject:
                    if (ValidateSubject(input, out var subject, out message))
                    {
                        profile.Subject = subject;
                        await turnContext.SendActivityAsync($"Event subject is{profile.Subject}.", null, null, cancellationToken);
                        await turnContext.SendActivityAsync("Please enter event body", null, null, cancellationToken);
                        flow.LastQuestionAsked = ConversationFlow.Question.Body;
                        break;
                    }
                    else
                    {
                        await turnContext.SendActivityAsync(message ?? "I'm sorry, I didn't understand that.", null, null, cancellationToken);
                        break;
                    }
                case ConversationFlow.Question.Body:
                    if (ValidateBody(input, out var body, out message))
                    {
                        profile.Body = body;
                        await turnContext.SendActivityAsync($"Event body consist of {profile.Body}.", null, null, cancellationToken);
                        await turnContext.SendActivityAsync("What time event starts", null, null, cancellationToken);
                        flow.LastQuestionAsked = ConversationFlow.Question.Start;
                        break;
                    }
                    else
                    {
                        await turnContext.SendActivityAsync(message ?? "I'm sorry, I didn't understand that.", null, null, cancellationToken);
                        break;
                    }

                case ConversationFlow.Question.Start:
                    if (ValidateStart(input, out var start, out message))
                    {
                        profile.Start = start;
                        await turnContext.SendActivityAsync($"Event starts at {profile.Start}.");
                        await turnContext.SendActivityAsync("What time event ends", null, null, cancellationToken);
                        flow.LastQuestionAsked = ConversationFlow.Question.End;
                        
                        break;
                    }
                    else
                    {
                        await turnContext.SendActivityAsync(message ?? "I'm sorry, I didn't understand that.", null, null, cancellationToken);
                        break;
                    }

                case ConversationFlow.Question.End:
                    if (ValidateEnd(input, out var end, out message))
                    {
                        profile.End = end;
                        await turnContext.SendActivityAsync($"Event ends at {profile.End}.", null, null, cancellationToken);
                        await turnContext.SendActivityAsync("Event Location", null, null, cancellationToken);
                        flow.LastQuestionAsked = ConversationFlow.Question.Location;
                        break;
                    }
                    else
                    {
                        await turnContext.SendActivityAsync(message ?? "I'm sorry, I didn't understand that.", null, null, cancellationToken);
                        break;
                    }

                case ConversationFlow.Question.Location:
                    if (ValidateLocation(input, out var location, out message))
                    {
                        profile.Location = location;
                        await turnContext.SendActivityAsync($"Event location is{profile.Location}.");
                       
                        flow.LastQuestionAsked = ConversationFlow.Question.None;
                        profile = new UserProfile();
                        break;
                    }
                    else
                    {
                        await turnContext.SendActivityAsync(message ?? "I'm sorry, I didn't understand that.", null, null, cancellationToken);
                        break;
                    }
            }
        }

        private static bool ValidateSubject(string input, out string subject, out string message)
        {
            subject = null;
            message = null;

            if (string.IsNullOrWhiteSpace(input))
            {
                message = "Please enter subject of event";
            }
            else
            {
                subject = input.Trim();
            }

            return message is null;
        }

        private static bool ValidateBody(string input, out string body, out string message)
        {
            body = null;
            message = null;

            if (string.IsNullOrWhiteSpace(input))
            {
                message = "Please enter body of event";
            }
            else
            {
                body = input.Trim();
            }

            return message is null;
        }

        private static bool ValidateStart(string input, out string start, out string message)
        {
            start = null;
            message = null;

            // Try to recognize the input as a date-time. This works for responses such as "11/14/2018", "9pm", "tomorrow", "Sunday at 5pm", and so on.
            // The recognizer returns a list of potential recognition results, if any.
            try
            {
                var results = DateTimeRecognizer.RecognizeDateTime(input, Culture.English);

                // Check whether any of the recognized date-times are appropriate,
                // and if so, return the first appropriate date-time. We're checking for a value at least an hour in the future.
                var earliest = DateTime.Now.AddHours(1.0);

                foreach (var result in results)
                {
                    // The result resolution is a dictionary, where the "values" entry contains the processed input.
                    var resolutions = result.Resolution["values"] as List<Dictionary<string, string>>;

                    foreach (var resolution in resolutions)
                    {
                        // The processed input contains a "value" entry if it is a date-time value, or "start" and
                        // "end" entries if it is a date-time range.
                        if (resolution.TryGetValue("value", out var dateString)
                            || resolution.TryGetValue("start", out dateString))
                        {
                            if (DateTime.TryParse(dateString, out var candidate)
                                && earliest < candidate)
                            {
                                start = candidate.ToShortDateString();
                                return true;
                            }
                        }
                    }
                }

                message = "I'm sorry, please enter a date at least an hour out.";
            }
            catch
            {
                message = "I'm sorry, I could not interpret that as an appropriate date. Please enter a date at least an hour out.";
            }

            return false;
        }

        private static bool ValidateEnd(string input, out string end, out string message)
        {
            end = null;
            message = null;

            // Try to recognize the input as a date-time. This works for responses such as "11/14/2018", "9pm", "tomorrow", "Sunday at 5pm", and so on.
            // The recognizer returns a list of potential recognition results, if any.
            try
            {
                var results = DateTimeRecognizer.RecognizeDateTime(input, Culture.English);

                // Check whether any of the recognized date-times are appropriate,
                // and if so, return the first appropriate date-time. We're checking for a value at least an hour in the future.
                var earliest = DateTime.Now.AddHours(1.0);

                foreach (var result in results)
                {
                    // The result resolution is a dictionary, where the "values" entry contains the processed input.
                    var resolutions = result.Resolution["values"] as List<Dictionary<string, string>>;

                    foreach (var resolution in resolutions)
                    {
                        // The processed input contains a "value" entry if it is a date-time value, or "start" and
                        // "end" entries if it is a date-time range.
                        if (resolution.TryGetValue("value", out var dateString)
                            || resolution.TryGetValue("start", out dateString))
                        {
                            if (DateTime.TryParse(dateString, out var candidate)
                                && earliest < candidate)
                            {
                                end= candidate.ToShortDateString();
                                return true;
                            }
                        }
                    }
                }

                message = "I'm sorry, please enter a date at least an hour out.";
            }
            catch
            {
                message = "I'm sorry, I could not interpret that as an appropriate date. Please enter a date at least an hour out.";
            }

            return false;
        }

        private static bool ValidateLocation(string input, out string location, out string message)
        {
            location = null;
            message = null;

            if (string.IsNullOrWhiteSpace(input))
            {
                message = "Please enter location of event";
            }
            else
            {
                location = input.Trim();
            }

            return message is null;
        }
    }
}
