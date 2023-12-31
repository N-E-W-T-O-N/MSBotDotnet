﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.AI.Luis;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Adaptive;
using Microsoft.Bot.Builder.Dialogs.Declarative;
using Microsoft.Bot.Builder.Dialogs.Declarative.Resources;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microsoft.BotBuilderSamples
{
    public class Startup
    {
        public Startup(IWebHostEnvironment env, IConfiguration configuration)
        {
            this.HostingEnvironment = env;
        }

        private IWebHostEnvironment HostingEnvironment { get; set; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();

            services.AddHttpClient().AddControllers().AddNewtonsoftJson();

            // Required for memory paths introduced by adaptive dialogs.
            ComponentRegistration.Add(new DialogsComponentRegistration());

            // Register declarative components
            ComponentRegistration.Add(new DeclarativeComponentRegistration());

            // Register adapive dialog components
            ComponentRegistration.Add(new AdaptiveComponentRegistration());

            // Add Language generation
            ComponentRegistration.Add(new LanguageGenerationComponentRegistration());

            // Add LUIS component
            ComponentRegistration.Add(new LuisComponentRegistration());

            // Create the Bot Framework Adapter with error handling enabled.
            services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();

            // Create the storage we'll be using for User and Conversation state. (Memory is great for testing purposes.)
            services.AddSingleton<IStorage, MemoryStorage>();

            // Create the User state. (Used in this bot's Dialog implementation.)
            services.AddSingleton<UserState>();

            // Create the Conversation state. (Used by the Dialog system itself.)
            services.AddSingleton<ConversationState>();

            // The Dialog that will be run by the bot.
            services.AddSingleton<RootDialog>();

            // Resource explorer to manage declarative resources for adaptive dialog
            var resourceExplorer = new ResourceExplorer().LoadProject(this.HostingEnvironment.ContentRootPath);
            services.AddSingleton(resourceExplorer);

            // Create the bot as a transient. In this case the ASP Controller is expecting an IBot.
            services.AddSingleton<IBot, DialogBot<RootDialog>>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseDefaultFiles()
                .UseStaticFiles()
                .UseRouting()
                .UseAuthorization()
                .UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                });

        }
    }
}
