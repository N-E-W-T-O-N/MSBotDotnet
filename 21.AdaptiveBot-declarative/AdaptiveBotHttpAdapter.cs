﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Microsoft.BotBuilderSamples
{
    public class AdaptiveBotHttpAdapter : CloudAdapter
    {
        public AdaptiveBotHttpAdapter(IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            ILogger<IBotFrameworkHttpAdapter> logger,
            IStorage storage, UserState userState, ConversationState conversationState)
            : base(configuration, httpClientFactory, logger)
        {
            this.Use(new RegisterClassMiddleware<IConfiguration>(configuration));
            this.UseStorage(storage);
            this.UseBotState(userState);
            this.UseBotState(conversationState);

            this.OnTurnError = async (turnContext, exception) =>
            {
                // Log any leaked exception from the application.
                logger.LogError($"Exception caught : {exception.Message}");

                // Send a catch-all apology to the user.
                await turnContext.SendActivityAsync("Sorry, it looks like something went wrong.");
                await turnContext.SendActivityAsync(exception.Message).ConfigureAwait(false);

                if (conversationState != null)
                {
                    try
                    {
                        // Delete the conversationState for the current conversation to prevent the
                        // bot from getting stuck in a error-loop caused by being in a bad state.
                        // ConversationState should be thought of as similar to "cookie-state" in a Web pages.
                        await conversationState.DeleteAsync(turnContext).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        logger.LogError($"Exception caught on attempting to Delete ConversationState : {e.Message}");
                    }
                }
            };
        }
    }
}
