namespace Chatbot;

public interface IAlertService
{
    // ----- async calls (use with "await" - MUST BE ON DISPATCHER THREAD) -----
    Task ShowAlertAsync(string title, string message, string cancel = "OK");
    Task<bool> ShowConfirmationAsync(string title, string message, string accept = "Yes", string cancel = "No");

    // ----- "Fire and forget" calls -----
    void ShowAlert(string title, string message, string cancel = "OK");

    /// <param name="callback">Action to perform afterwards.</param>
    void ShowConfirmation(string title, string message, Action<bool> callback,
                          string accept = "Yes", string cancel = "No");
}

internal sealed class AlertService : IAlertService
{
    // ----- async calls (use with "await" - MUST BE ON DISPATCHER THREAD) -----
    public Task ShowAlertAsync(string title, string message, string cancel = "OK") =>
        Application.Current!.MainPage!.DisplayAlert(title, message, cancel);

    public Task<bool> ShowConfirmationAsync(string title, string message, string accept = "Yes", string cancel = "No") => 
        Application.Current!.MainPage!.DisplayAlert(title, message, accept, cancel);


    // ----- "Fire and forget" calls -----

    /// <summary>
    /// "Fire and forget". Method returns BEFORE showing alert.
    /// </summary>
    public void ShowAlert(string title, string message, string cancel = "OK") =>
        Application.Current?.MainPage?.Dispatcher.Dispatch(async () => await ShowAlertAsync(title, message, cancel));

    /// <summary>
    /// "Fire and forget". Method returns BEFORE showing alert.
    /// </summary>
    /// <param name="callback">Action to perform afterwards.</param>
    public void ShowConfirmation(string title, string message, Action<bool> callback, string accept = "Yes", string cancel = "No") =>
        Application.Current?.MainPage?.Dispatcher.Dispatch(async () =>
            callback(await ShowConfirmationAsync(title, message, accept, cancel)));
}
