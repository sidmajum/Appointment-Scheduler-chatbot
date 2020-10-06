using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BotAuthenticationMSGraph
{
    public class ConversationFlow
    {
        // Identifies the last question asked.
        public enum Question
        {
            Subject,
            Body,
            Start,
            End,
            Location,
            None,
        }

        // The last question asked.
        public Question LastQuestionAsked { get; set; } = Question.None;
    }
}

