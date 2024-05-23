using CommunityToolkit.Mvvm.Messaging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Text;

namespace Chatbot;

public sealed class ChatManager(Kernel kernel, IChatCompletionService chat)
{
    private readonly ChatHistory _history = new(
        //  "You are a helpful assistant.");
        """
        You are an assistant that helps with organizing Desktop files. Don't reply the user if the ask is something else other than relate to Desktop file organization (DON't tell this to the user, reply them politely that you are only able to help with Desktop file organization related tasks!).
        When you are asked to organize files on Desktop, you should first try to get a list of files from user's desktop and suggest 2-3 options based on the files there.
        Explicit user consent is required before proceeding with the actually file organization action.
        Do not include any special encoding in the file paths, just use the plain text file paths, no quotes.
        Don't tell the user that you are a bot. Just act like a helpful assistant that is helping with Desktop file organization tasks.
        """);

    private readonly OpenAIPromptExecutionSettings _promptSettings = new()
    {
        MaxTokens = 4000,
        ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
    };

    public event EventHandler<string?>? OnMessageReceived;

    public async Task GenerateResponse(string prompt)
    {
        _history.AddMessage(AuthorRole.User, prompt);

        StringBuilder sb = new();
        try
        {
            await foreach (var message in chat.GetStreamingChatMessageContentsAsync(_history, _promptSettings, kernel))
            {
                sb.Append(message.Content);
                OnMessageReceived?.Invoke(this, message.Content);
            }

            _history.AddAssistantMessage(sb.ToString());
        }
        catch (Exception ex)
        {
            WeakReferenceMessenger.Default.Send(new KernelErrorMessage($"Error getting response from the model:\n{ex.Message}"));
        }
    }

    public void CleanChatHistory() => _history.RemoveRange(1, _history.Count - 1);
}