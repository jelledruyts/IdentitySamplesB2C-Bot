using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IdentitySamplesB2C.Bot.Dialogs
{
    public class MainDialog : LogoutDialog
    {
        protected readonly ILogger Logger;

        public MainDialog(IConfiguration configuration, ILogger<MainDialog> logger)
            : base(nameof(MainDialog), configuration["ConnectionName"])
        {
            Logger = logger;
            this.Logger.LogInformation("Constructor-Begin");

            AddDialog(new OAuthPrompt(
                nameof(OAuthPrompt),
                new OAuthPromptSettings
                {
                    ConnectionName = ConnectionName,
                    Text = "Please Sign In",
                    Title = "Sign In",
                    Timeout = 300000, // User has 5 minutes to login (1000 * 60 * 5)
                }));

            AddDialog(new ConfirmPrompt(nameof(ConfirmPrompt)));

            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                PromptStepAsync,
                LoginStepAsync,
                DisplayTokenPhase1Async,
                DisplayTokenPhase2Async,
            }));

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
            this.Logger.LogInformation("Constructor-End");
        }

        private async Task<DialogTurnResult> PromptStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            this.Logger.LogInformation("PromptStepAsync-Begin");
            return await stepContext.BeginDialogAsync(nameof(OAuthPrompt), null, cancellationToken);
        }

        private async Task<DialogTurnResult> LoginStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            this.Logger.LogInformation("LoginStepAsync-Begin");

            // Workaround for https://github.com/microsoft/BotFramework-WebChat/issues/3874
            // which causes a 5-second delay after having signed in.
            stepContext.Context.Activity.Id = "";
            
            // Get the token from the previous step. Note that we could also have gotten the
            // token directly from the prompt itself. There is an example of this in the next method.
            var tokenResponse = (TokenResponse)stepContext.Result;
            if (tokenResponse != null)
            {
                this.Logger.LogInformation("LoginStepAsync-TokenResponse");
                await stepContext.Context.SendActivityAsync(MessageFactory.Text("You are now logged in."), cancellationToken);
                this.Logger.LogInformation("LoginStepAsync-OK-ActivitySent");
                return await stepContext.PromptAsync(nameof(ConfirmPrompt), new PromptOptions { Prompt = MessageFactory.Text("Would you like to view your token?") }, cancellationToken);
            }

            this.Logger.LogInformation("LoginStepAsync-Fail");
            await stepContext.Context.SendActivityAsync(MessageFactory.Text("Login was not successful please try again."), cancellationToken);
            this.Logger.LogInformation("LoginStepAsync-Fail-ActivitySent");
            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }

        private async Task<DialogTurnResult> DisplayTokenPhase1Async(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            this.Logger.LogInformation("DisplayTokenPhase1Async-Begin");
            await stepContext.Context.SendActivityAsync(MessageFactory.Text("Thank you."), cancellationToken);
            this.Logger.LogInformation("DisplayTokenPhase1Async-ActivitySent");

            var result = (bool)stepContext.Result;
            if (result)
            {
                // Call the prompt again because we need the token. The reasons for this are:
                // 1. If the user is already logged in we do not need to store the token locally in the bot and worry
                // about refreshing it. We can always just call the prompt again to get the token.
                // 2. We never know how long it will take a user to respond. By the time the
                // user responds the token may have expired. The user would then be prompted to login again.
                //
                // There is no reason to store the token locally in the bot because we can always just call
                // the OAuth prompt to get the token or get a new token if needed.
                this.Logger.LogInformation("DisplayTokenPhase1Async-BeginDialog");
                return await stepContext.BeginDialogAsync(nameof(OAuthPrompt), cancellationToken: cancellationToken);
            }

            this.Logger.LogInformation("DisplayTokenPhase1Async-EndDialog");
            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }

        private async Task<DialogTurnResult> DisplayTokenPhase2Async(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            this.Logger.LogInformation("DisplayTokenPhase2Async-Begin");
            var tokenResponse = (TokenResponse)stepContext.Result;
            if (tokenResponse != null)
            {
                this.Logger.LogInformation("DisplayTokenPhase2Async-TokenResponse");
                await stepContext.Context.SendActivityAsync(MessageFactory.Text($"Here is your token {tokenResponse.Token}"), cancellationToken);
                this.Logger.LogInformation("DisplayTokenPhase2Async-OK-Sent");
            }

            this.Logger.LogInformation("DisplayTokenPhase2Async-Ending");
            return await stepContext.EndDialogAsync(cancellationToken: cancellationToken);
        }
    }
}