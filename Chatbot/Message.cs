using CommunityToolkit.Mvvm.ComponentModel;

namespace Chatbot;

public partial class Message : ObservableObject
{
    [ObservableProperty]
    string? content;

    [ObservableProperty]
    string? role;

    [ObservableProperty]
    bool pluginInvoked = false;

    [ObservableProperty]
    string? pluginUsage;
}

internal class MessageTemplateSelector : DataTemplateSelector
{
    public DataTemplate? UserMessageTemplate { get; set; }
    public DataTemplate? BotMessageTemplate { get; set; }

    protected override DataTemplate? OnSelectTemplate(object item, BindableObject container)
    {
        if (item is Message message)
        {
            return message.Role switch
            {
                "User" => UserMessageTemplate,
                "Assistant" => BotMessageTemplate,
                _ => throw new ArgumentException("Invalid role", nameof(item))
            };
        }

        return null;
    }
}