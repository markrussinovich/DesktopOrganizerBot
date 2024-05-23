namespace Chatbot;

public partial class MainPage : ContentPage
{
    private readonly ChatHistoryViewModel viewModel;

    public MainPage(ChatHistoryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = this.viewModel = viewModel;
    }

    public async void OnMessageAdded(object sender, ElementEventArgs e)
    {
        if (sender is CollectionView collectionView && viewModel.Messages.Count != 0)
        {
            var lastMessage = viewModel.Messages.Count - 1;
            await Task.Delay(100);
            collectionView.ScrollTo(lastMessage, animate: false, position: ScrollToPosition.MakeVisible);
        }
    }
}
