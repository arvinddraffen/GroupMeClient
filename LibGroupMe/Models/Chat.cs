﻿using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LibGroupMe.Models
{
    /// <summary>
    /// <see cref="Chat"/> represents a GroupMe Direct Message (or Chat) with another user
    /// </summary>
    public class Chat
    {
        /// <summary>
        /// Gets or Sets the <see cref="Member"/> that this chat is being held with
        /// </summary>
        [JsonProperty("other_user")]
        public Member OtherUser { get; internal set; }

        /// <summary>
        /// Gets or Sets the Unix Timestamp for when this chat was created
        /// </summary>
        [JsonProperty("created_at")]
        public int CreatedAtUnixTime { get; internal set; }

        /// <summary>
        /// Gets the Date and Time when this chat was created
        /// </summary>
        public DateTime CreatedAtTime => DateTimeOffset.FromUnixTimeSeconds(this.CreatedAtUnixTime).ToLocalTime().DateTime;

        /// <summary>
        /// Gets or Sets the Unix Timestamp for when this chat was last updated
        /// </summary>
        [JsonProperty("updated_at")]
        public int UpdatedAtUnixTime { get; internal set; }

        /// <summary>
        /// Gets the Date and Time when this chat was last updated
        /// </summary>
        public DateTime UpdatedAtTime => DateTimeOffset.FromUnixTimeSeconds(this.UpdatedAtUnixTime).ToLocalTime().DateTime;

        /// <summary>
        /// Gets or Sets the latest message in this chat
        /// </summary>
        [JsonProperty("last_message")]
        public Message LatestMessage { get; internal set; }

        /// <summary>
        /// The <see cref="GroupMeClient"/> that manages this <see cref="Chat"/>
        /// </summary>
        internal GroupMeClient Client;

        /// <summary>
        /// Returns a set of messages from a this Direct Message / Chat
        /// </summary>
        /// <param name="mode">The method that should be used to determine the set of messages returned </param>
        /// <param name="messageId">The Message Id that will be used by the sorting mode set in <paramref name="mode"/></param>
        /// <returns>A list of <see cref="Message"/></returns>
        public async Task<IList<Message>> GetMessagesAsync(MessageRetreiveMode mode = MessageRetreiveMode.None, string messageId = "")
        {
            var request = new RestRequest($"/direct_messages", Method.GET);
            request.AddParameter("token", this.Client.AuthToken);
            request.AddParameter("other_user_id", this.OtherUser.Id);
            switch (mode)
            {
                case MessageRetreiveMode.AfterId:
                    request.AddParameter("after_id", messageId);
                    break;

                case MessageRetreiveMode.SinceId:
                    request.AddParameter("since_id", messageId);
                    break;

            }

            var cancellationTokenSource = new CancellationTokenSource();
            var restResponse = await this.Client.ApiClient.ExecuteTaskAsync(request, cancellationTokenSource.Token);

            if (restResponse.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var results = JsonConvert.DeserializeObject<ChatMessagesList>(restResponse.Content);
                return results.Response.Messages;
            }
            else
            {
                throw new System.Net.WebException($"Failure retreving Messages from Chat. Status Code {restResponse.StatusCode}");
            }
        }

        /// <summary>
        /// Sends a message to this <see cref="Chat"/>
        /// </summary>
        /// <param name="message">The message to send</param>
        /// <returns></returns>
        public async Task<bool> SendMessage(Message message)
        {
            var request = new RestRequest($"/direct_messages", Method.POST);
            request.AddParameter("token", this.Client.AuthToken);

            request.AddJsonBody(message);

            var cancellationTokenSource = new CancellationTokenSource();
            var restResponse = await this.Client.ApiClient.ExecuteTaskAsync(request, cancellationTokenSource.Token);

            return (restResponse.StatusCode == System.Net.HttpStatusCode.OK);
        }
    }
}
