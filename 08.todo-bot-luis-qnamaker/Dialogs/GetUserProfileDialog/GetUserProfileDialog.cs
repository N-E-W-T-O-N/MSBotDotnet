﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AdaptiveExpressions;
using AdaptiveExpressions.Properties;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.AI.Luis;
using Microsoft.Bot.Builder.AI.QnA.Recognizers;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Adaptive;
using Microsoft.Bot.Builder.Dialogs.Adaptive.Actions;
using Microsoft.Bot.Builder.Dialogs.Adaptive.Conditions;
using Microsoft.Bot.Builder.Dialogs.Adaptive.Generators;
using Microsoft.Bot.Builder.Dialogs.Adaptive.Input;
using Microsoft.Bot.Builder.Dialogs.Adaptive.Recognizers;
using Microsoft.Bot.Builder.Dialogs.Adaptive.Templates;
using Microsoft.Bot.Builder.LanguageGeneration;
using Microsoft.Extensions.Configuration;

namespace Microsoft.BotBuilderSamples
{
    public class GetUserProfileDialog : ComponentDialog
    {
        private readonly IConfiguration configuration;
        private Templates _templates;
        private AdaptiveDialog _getUserProfileDialog;

        public GetUserProfileDialog(IConfiguration configuration)
            : base(nameof(GetUserProfileDialog))
        {
            this.configuration = configuration;
            _templates = Templates.ParseFile(Path.Combine(".", "Dialogs", "GetUserProfileDialog", "GetUserProfileDialog.lg"));

            // Create instance of adaptive dialog.
            this._getUserProfileDialog = new AdaptiveDialog(nameof(GetUserProfileDialog))
            {
                Recognizer = CreateCrossTrainedRecognizer(this.configuration),
                Generator = new TemplateEngineLanguageGenerator(_templates),
                Triggers = new List<OnCondition>()
                {
                    // Actions to execute when this dialog begins. This dialog will attempt to fill user profile.
                    // Each adaptive dialog can have its own recognizer. The LU definition for this dialog is under GetUserProfileDialog.lu
                    // This dialog supports local intents. When you say things like 'why do you need my name' or 'I will not give you my name'
                    // it uses its own recognizer to handle those.
                    // It also demonstrates the consultation capability of adaptive dialog. When the local recognizer does not come back with
                    // a high-confidence recognition, this dialog will defer to the parent dialog to see if it wants to handle the user input.
                    new OnBeginDialog()
                    {
                        Actions = new List<Dialog>()
                        {
                            new SetProperties()
                            {
                                Assignments = new List<PropertyAssignment>()
                                {
                                    new PropertyAssignment()
                                    {
                                        Property = "user.profile.name",
                                        
                                        // Whenever an adaptive dialog begins, any options passed in to the dialog via 'BeginDialog' are available through dialog.xxx scope.
                                        // Coalesce is a prebuilt function available as part of Adaptive Expressions. Take the first non-null value.
                                        // @EntityName is a short-hand for turn.recognized.entities.<EntityName>. Other useful short-hands are 
                                        //     #IntentName is a short-hand for turn.intents.<IntentName>
                                        //     $PropertyName is a short-hand for dialog.<PropertyName>
                                        Value = "=coalesce(dialog.userName, @userName, @personName)"
                                    },
                                    new PropertyAssignment()
                                    {
                                        Property = "user.profile.age",
                                        Value = "=coalesce(dialog.userAge, @age)"
                                    }
                                }
                            },
                            new TextInput()
                            {
                                Property = "user.profile.name",
                                Prompt = new ActivityTemplate("${AskFirstName()}"),
                                Validations = new List<BoolExpression>()
                                {
                                    // Name must be 3-50 characters in length.
                                    // Validations are expressed using Adaptive expressions
                                    // You can access the current candidate value for this input via 'this.value'
                                    "count(this.value) >= 3",
                                    "count(this.value) <= 50"
                                },
                                InvalidPrompt = new ActivityTemplate("${AskFirstName.Invalid()}"),
                                
                                // Because we have a local recognizer, we can use it to extract entities.
                                // This enables users to say things like 'my name is vishwac' and we only take 'vishwac' as the name.
                                Value = "=@personName",
                                
                                // We are going to allow any interruption for a high confidence interruption intent classification .or.
                                // when we do not get a value for the personName entity. 
                                AllowInterruptions = "turn.recognized.score >= 0.9 || !@personName",
                            },
                            new NumberInput()
                            {
                                Property = "user.profile.age",
                                Prompt = new ActivityTemplate("${AskUserAge()}"),
                                Validations = new List<BoolExpression>()
                                {
                                    // Age must be within 1-150.
                                    "int(this.value) >= 1",
                                    "int(this.value) <= 150"
                                },
                                InvalidPrompt = new ActivityTemplate("${AskUserAge.Invalid()}"),
                                UnrecognizedPrompt = new ActivityTemplate("${AskUserAge.Unrecognized()}"),

                                // We have both a number recognizer as well as age prebuilt entity recognizer added. So take either one we get.
                                // LUIS returns a complex type for prebuilt age entity. Take just the number value.
                                Value = "=coalesce(@age.number, @number)",

                                // Allow interruption if we do not get either an age or a number.
                                AllowInterruptions = "!@age && !@number"
                            },
                            new SendActivity("${ProfileReadBack()}")
                        }
                    },
                    new OnIntent()
                    {
                        Intent = "NoValue",
                        
                        // Only do this only on high confidence recognition
                        Condition = "#NoValue.Score >= 0.9",
                        Actions = new List<Dialog>()
                        {
                            new IfCondition()
                            {
                                Condition = "user.profile.name == null",
                                Actions = new List<Dialog>()
                                {
                                    new SetProperty()
                                    {
                                        Property = "user.profile.name",
                                        Value = "Human"
                                    },
                                    new SendActivity("${NoValueForUserNameReadBack()}")
                                },
                                ElseActions = new List<Dialog>()
                                {
                                    new SetProperty()
                                    {
                                        Property = "user.profile.age",
                                        Value = "30"
                                    },
                                    new SendActivity("${NoValueForUserAgeReadBack()}")
                                }
                            }
                        }
                    },
                    new OnIntent()
                    {
                        Intent = "GetInput",
                        Actions = new List<Dialog>()
                        {
                            new SetProperties()
                            {
                                Assignments = new List<PropertyAssignment>()
                                {
                                    new PropertyAssignment()
                                    {
                                        Property = "user.profile.name",
                                        Value = "=@personName"
                                    },
                                    new PropertyAssignment()
                                    {
                                        Property = "user.profile.age",
                                        Value = "=coalesce(@age, @number)"
                                    }
                                }
                            }
                        }
                    },
                    new OnIntent()
                    {
                        Intent = "Restart",
                        Actions = new List<Dialog>()
                        {
                            new DeleteProperty()
                            {
                                Property = "user.profile"
                            }
                        }
                    },
                    // Help and chitchat is handled by qna
                    new OnQnAMatch
                    {
                        Actions = new List<Dialog>()
                        {
                            new CodeAction(ResolveAndSendQnAAnswer)
                        }
                    }
                }
            };

            // Add named dialogs to the DialogSet. These names are saved in the dialog state.
            AddDialog(this._getUserProfileDialog);

            // The initial child Dialog to run.
            InitialDialogId = nameof(GetUserProfileDialog);
        }

        private async Task<DialogTurnResult> ResolveAndSendQnAAnswer(DialogContext dc, System.Object options)
        {
            var exp1 = Expression.Parse("@answer").TryEvaluate(dc.State).value;
            var resVal = await this._getUserProfileDialog.Generator.GenerateAsync(dc, exp1.ToString(), dc.State);
            await dc.Context.SendActivityAsync(ActivityFactory.FromObject(resVal));
            return await dc.EndDialogAsync(options);
        }

        private static Recognizer CreateCrossTrainedRecognizer(IConfiguration configuration)
        {
            return new CrossTrainedRecognizerSet()
            {
                Recognizers = new List<Recognizer>()
                {
                    CreateLuisRecognizer(configuration),
                    CreateQnAMakerRecognizer(configuration)
                }
            };
        }

        private static Recognizer CreateQnAMakerRecognizer(IConfiguration configuration)
        {
            if (string.IsNullOrEmpty(configuration["qna:GetUserProfileDialog_en_us_qna"]) || string.IsNullOrEmpty(configuration["QnAHostName"]) || string.IsNullOrEmpty(configuration["QnAEndpointKey"]))
            {
                throw new Exception("NOTE: QnA Maker is not configured for GetUserProfileDialog. Please follow instructions in README.md to add 'qna:GetUserProfileDialog_en_us_qna', 'QnAHostName' and 'QnAEndpointKey' to the appsettings.json file.");
            }

            return new QnAMakerRecognizer()
            {
                HostName = configuration["QnAHostName"],
                EndpointKey = configuration["QnAEndpointKey"],
                KnowledgeBaseId = configuration["qna:GetUserProfileDialog_en_us_qna"],

                // property path that holds qna context
                Context = "dialog.qnaContext",

                // Property path where previous qna id is set. This is required to have multi-turn QnA working.
                QnAId = "turn.qnaIdFromPrompt",

                // Disable teletry logging
                LogPersonalInformation = false,

                // Enable to automatically including dialog name as meta data filter on calls to QnA Maker.
                IncludeDialogNameInMetadata = true,

                // Id needs to be QnA_<dialogName> for cross-trained recognizer to work.
                Id = $"QnA_{nameof(GetUserProfileDialog)}"
            };
        }
        private static Recognizer CreateLuisRecognizer(IConfiguration Configuration)
        {
            if (string.IsNullOrEmpty(Configuration["luis:GetUserProfileDialog_en_us_lu"]) || string.IsNullOrEmpty(Configuration["LuisAPIKey"]) || string.IsNullOrEmpty(Configuration["LuisAPIHostName"]))
            {
                throw new Exception("NOTE: LUIS is not configured for Get user profile dialog. To enable all capabilities, add 'LuisAppId-RootDialog', 'LuisAPIKey' and 'LuisAPIHostName' to the appsettings.json file.");
            }

            return new LuisAdaptiveRecognizer()
            {
                ApplicationId = Configuration["luis:GetUserProfileDialog_en_us_lu"],
                Endpoint = Configuration["LuisAPIHostName"],
                EndpointKey = Configuration["LuisAPIKey"],

                // Id needs to be LUIS_<dialogName> for cross-trained recognizer to work.
                Id = $"LUIS_{nameof(GetUserProfileDialog)}"
            };
        }
    }
}
