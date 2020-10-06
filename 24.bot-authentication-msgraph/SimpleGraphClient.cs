// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using BotAuthenticationMSGraph;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Graph;
using Microsoft.Recognizers.Text;
using Microsoft.Recognizers.Text.DateTime;

namespace Microsoft.BotBuilderSamples
{
    // This class is a wrapper for the Microsoft Graph API
    // See: https://developer.microsoft.com/en-us/graph
    public class SimpleGraphClient
    {
        private readonly GraphServiceClient _client;
        private readonly string _token;

        private SimpleGraphClient(GraphServiceClient client)
        {
            _client = client;
        }

        public SimpleGraphClient(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentNullException(nameof(token));
            }

            _token = token;
        }

        // Get information about the user.
        public async Task<User> GetMeAsync()
        {
            var graphClient = GetAuthenticatedClient(_token);
            var me = await graphClient._client.Me.Request().GetAsync();
            return me;
        }

        // Get an Authenticated Microsoft Graph client using the token issued to the user.
        public static SimpleGraphClient GetAuthenticatedClient(string token)
        {
            return new SimpleGraphClient(new GraphServiceClient(
                new DelegateAuthenticationProvider(
                    requestMessage =>
                    {
                        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", token);
                        requestMessage.Headers.Add("Prefer", "outlook.timezone=\"" + TimeZoneInfo.Local.Id + "\"");

                        return Task.CompletedTask;
                    })));
        }
        public async Task<IEnumerable<Event>> GetCalendarEvents()
        {
            List<Option> options = new List<Option>();

            options.Add(new QueryOption("startDateTime", DateTime.Now.ToString("o").Split("+")[0]));
            options.Add(new QueryOption("endDateTime", DateTime.Now.AddDays(7).ToString("o").Split("+")[0]));
            options.Add(new HeaderOption("Prefer", "outlook.timezone=\"" + TimeZoneInfo.Local.Id + "\""));



            var calendarView = await _client.Me.Calendar.CalendarView.Request(options).GetAsync();

            return calendarView.CurrentPage;




        }

        public async Task<IEnumerable<Event>> GetGroupCalendarEvents()
        {
            List<Option> options = new List<Option>();

            options.Add(new QueryOption("startDateTime", DateTime.Now.ToString("o").Split("+")[0]));
            options.Add(new QueryOption("endDateTime", DateTime.Now.AddDays(7).ToString("o").Split("+")[0]));
            options.Add(new HeaderOption("Prefer", "outlook.timezone=\"" + TimeZoneInfo.Local.Id + "\""));



            var groupcalendarView = _client.Groups["9efa069f-79ee-447e-beb4-65a9dcbcb62f"].Calendar.CalendarView.Request(options).GetAsync().Result;




            return groupcalendarView.CurrentPage;




        }

        public async Task SetAppointment(string title, DateTimeTimeZone start, DateTimeTimeZone end)
        {
            var @event = new Event
            {
                Subject = title,
                Body = new ItemBody
                {
                    ContentType = BodyType.Html,
                    Content = ""
                },
                Start = start,
                End = end,
                Location = new Location
                {
                    DisplayName = title
                },

            };

            //var @event1 = new Event
            //{
            //    Subject = "Let's go for lunch",
            //    Body = new ItemBody
            //    {
            //        ContentType = BodyType.Html,
            //        Content = "Does mid month work for you?"
            //    },
            //    Start = new DateTimeTimeZone
            //    {
            //        DateTime = "2020-08-28T12:00:00",
            //        TimeZone = "Pacific Standard Time"
            //    },
            //    End = new DateTimeTimeZone
            //    {
            //        DateTime = "2020-08-28T14:00:00",
            //        TimeZone = "Pacific Standard Time"
            //    },
            //    Location = new Location
            //    {
            //        DisplayName = "Harry's Bar"
            //    },
            //};


            // Create and add the event.
            var user = _client.Me.Calendar.Events.Request();
            await user.AddAsync(@event);
           // await user.AddAsync(@event1);

        }

        public async Task CreateGroupCalendar()
        {

            var calendarGroup = new CalendarGroup
            {
                Name = "DEMO-GROUP-CALENDAR",
                ClassId = Guid.Parse("c8ea0ed1-3834-4d80-ba48-9552c4d2e689"),
                ChangeKey = "changeKey-value"
            };

            await _client.Me.CalendarGroups.Request().AddAsync(calendarGroup);


        }




    }
}
