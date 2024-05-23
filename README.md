# Scott and Mark learn AI

This is the code repo used in the Scott and Mark learn AI session at Microsoft Build 2024. Check the session recording here: https://www.youtube.com/watch?v=KKWPSkYN3vw

## Config Azure OpenAI endpoints
The chatbot uses Azure OpenAI endpoints. To use the app, config your model, Azure OpenAI endpoints and keys in `Resources\appsettings.json` first.

## Demo desktop setup 

To use this, you need to set up your Desktop first.

1. Create a non OneDrive folder for this demo as root folder.
2. Create a Desktop folder under the root folder.
3. Run `git init` in the root folder so that we can restore the Desktop folder if things go wrong. 
4. Right click `Desktop` in the left tree view from your Explorer window, go to Properties, Location tab, set the Desktop path to the desktop folder in the root folder.
5. Move the files and folders to the new Desktop folder.
6. Run the app, first click the Backup button to back up the state that you might want to restore later. You can also do this via git commands directly. 

## Run Aspire dashboard

1. Install Docker Desktop
2. Run this command
    ```
    docker run --rm -it -p 18888:18888 -p 4317:18889 -d --name aspire-dashboard mcr.microsoft.com/dotnet/aspire-dashboard:8.0.0
    ```
3. Open Aspire dashboard from `http://localhost:18888/`.
4. Login token can be found at the beginning of the container log like this `Login to the dashboard at http://0.0.0.0:18888/login?t=04cc5ebb75c2b22e39173b3dab2a50af`. Copy the token after `login?` to login.

## Project structure
Main files to interact with:
- MauiProgram.cs - initialize services, setup kernel model entpoints, logging
- ChatManager.cs - manage metaprompt to GPT
- OrganizeDesktopPlugin.cs - plugin functions for GPT to call
- ChatHistoryViewModel.cs - view model that manages chat history and other stuff for UI

## Local model server setup
Check news on [Announcing the AI Toolkit for Visual Studio Code](https://techcommunity.microsoft.com/t5/microsoft-developer-community/announcing-the-ai-toolkit-for-visual-studio-code/ba-p/4146473).

See news from Microsoft for the to-be-released local model server. 



