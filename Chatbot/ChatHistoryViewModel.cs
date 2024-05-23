using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;

namespace Chatbot;

public partial class ChatHistoryViewModel : ObservableObject
{
    private static readonly ActivitySource s_activitySource = new("DesktopOrganizerBot");

    public MvvmHelpers.ObservableRangeCollection<Message> Messages { get; set; } = [];

    [ObservableProperty]
    private string? text;

    private readonly ChatManager _manager;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmit))]
    private bool processingBotResponse = false;

    public bool CanSubmit => !ProcessingBotResponse;

    public ChatHistoryViewModel(ChatManager manager)
    {
        _manager = manager;
        _manager.OnMessageReceived += (object? sender, string? streamingResponse) =>
        {
            ProcessingBotResponse = true;
            GetLastBotMessage().Content += streamingResponse;
        };

        WeakReferenceMessenger.Default.Register<PluginInUseMessage>(this, (r, m) =>
        {
            var lastMessage = GetLastBotMessage();
            lastMessage.PluginInvoked = true;
            lastMessage.PluginUsage = $"Using plugin: {m.Value["pluginName"]}--{m.Value["functionName"]}";
        });

        WeakReferenceMessenger.Default.Register<KernelErrorMessage>(this, (r, m) =>
            App.AlertService?.ShowAlert("Error", m.Value));
    }

    [RelayCommand]
    private void ClearMessages()
    {
        Messages.Clear();
        _manager.CleanChatHistory();
    }

    [RelayCommand]
    private void Backup() => OrganizeDesktopPlugin.Backup();

    [RelayCommand]
    private async Task Submit()
    {
        using var activity = s_activitySource.StartActivity("GenerateResponse");

        string? prompt = Text;
        if (prompt is null)
        {
            return;
        }
        Messages.Add(new Message() { Content = $"You: {Text}", Role = "User" });
        Text = string.Empty;

        Messages.Add(new Message() { Content = $"Bot: ", Role = "Assistant" });
        ProcessingBotResponse = true;
        await _manager.GenerateResponse(prompt ?? "");
        ProcessingBotResponse = false;
    }

    private Message GetLastBotMessage() => Messages.Last(m => m.Role == "Assistant");
}
