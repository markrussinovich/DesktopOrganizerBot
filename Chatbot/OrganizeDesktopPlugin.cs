using CommunityToolkit.Mvvm.Messaging;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Chatbot;

internal sealed class OrganizeDesktopPlugin
{
    private const int MaxTokens = 2000;

    private readonly KernelFunction _organizeFiles;
    private readonly KernelFunction _summarizeFile;
    private readonly PromptExecutionSettings _settings = new()
    {
        ExtensionData = new Dictionary<string, object>
        {
            { "max_tokens", MaxTokens },
            { "temperature", 0.7 }
        }
    };

    private static readonly string _desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

    public OrganizeDesktopPlugin()
    {
        _organizeFiles = KernelFunctionFactory.CreateFromPrompt("""
            Based on files and their relative paths in 'FILE TO ORGANIZE', provide suggestions on how to organize these files to different folders. 
            Make sure to follow user's preference on how to organize the files. User's preference will be provided in 'User Preference'.
            Return the organized file structure in json. 

            EXAMPLE INPUT:
            file1.txt, image/image1.jpg, software.exe, document/document1.docx

            EXAMPLE OUTPUT:
            {
                'images': ['image1.jpg'],
                'documents': ['document1.docx'],
                'others': ['file1.txt', 'software.exe']
            }

            BEGIN FILE TO ORGANIZE
            {{$fileList}}
            END FILE TO ORGANIZE

            User Preference: {{$userPreference}}

            OUTPUT:
            """,
            functionName: "OrganizeFiles",
            description: "Given a list of files on Desktop, suggests a new file structure to organize these files in JSON");

        _summarizeFile = KernelFunctionFactory.CreateFromPrompt("""
            Summarize this file:
            ```
            {{$input}}
            ```
            """,
            functionName: "SummarizeFile",
            description: "Summarize the content of a file");
    }

    [KernelFunction,
     Description("Get the new file and folder structure based on user's preference. This doesn't act on the files.")]
    public async Task<string> GetNewFileStructure(Kernel kernel, [Description("List of files to organize")] string fileList, [Description("User preferred way to organize their desktop files")] string userPreference)
    {
        ReportPluginInUse();

        if (!string.IsNullOrEmpty(userPreference))
        {
            try
            {
                KernelArguments args = new(_settings)
                {
                    { "fileList", fileList },
                    { "userPreference", userPreference }
                };

                return (await _organizeFiles.InvokeAsync<string>(kernel, args))!;
            }
            catch (Exception e)
            {
                return $"Error organizing files. {e.Message}";
            }
        }

        return "Please provide a user preference to proceed with file organization.";
    }

    [KernelFunction,
     Description("Summarize the contents of a file")]
    public async Task<string> SummarizeFile(Kernel kernel, [Description("Relative file path on Desktop")] string filePath)
    {
        ReportPluginInUse();

        if (filePath is null or "")
        {
            return $"Error reading file {filePath} Please provide a file path to read the content.";
        }

        string content = File.ReadAllText(Path.Combine(_desktopPath, filePath));
        if (content.Length > 1000)
        {
            content = string.Concat(content.AsSpan(0, 1000), "...");
        }

        if (!string.IsNullOrEmpty(content))
        {
            try
            {
                KernelArguments args = new() { { "input", content } };

                //Add this line to switch to a local model for summarization
                args.ExecutionSettings = new Dictionary<string, PromptExecutionSettings>()
                {
                    { "localmodel", _settings }
                };

                //return (await _summarizeFile.InvokeAsync<string>(kernel, args))!;

                StringBuilder response = new();
                await foreach (var update in _summarizeFile.InvokeStreamingAsync<string>(kernel, args))
                {
                    response.Append(update);
                }
                return response.ToString();
            }
            catch (Exception e)
            {
                return $"Error summarizing file {filePath}. {e.Message}";
            }
        }

        return "File is empty or failed to read contents from file.";
    }

    [KernelFunction,
     Description("Get a list of files with their relative path from Desktop, separated by commas")]
    public string GetDesktopFiles(
      [Description("Filter on file extension when getting files")] string fileExtensionFilter = "All")
    {
        ReportPluginInUse();

        var allFiles = GetFilesExcludingGitRepos(_desktopPath, "*", SearchOption.AllDirectories).ToArray();
        for (int i = 0; i < allFiles.Length; i++)
        {
            allFiles[i] = Path.GetRelativePath(_desktopPath, allFiles[i]);
        }

        if (fileExtensionFilter != "All")
        {
            allFiles = allFiles.Where(f => Path.GetExtension(f).Contains(fileExtensionFilter)).ToArray();
        }

        return string.Join(",", from file in allFiles
                                select Path.GetRelativePath(_desktopPath, file));
    }

    [KernelFunction,
     Description("Read the first 1000 characters from a file.")]
    public string ReadFileContent([Description("Relative file path on Desktop")] string filePath)
    {
        ReportPluginInUse();

        if (filePath is null or "")
        {
            return $"Error reading file {filePath} Please provide a file path to read the content.";
        }

        string content = File.ReadAllText(Path.Combine(_desktopPath, filePath));
        if (content.Length > 1000)
        {
            content = string.Concat(content.AsSpan(0, 1000), "...");
        }

        return content;
    }

    [KernelFunction,
     Description("Move a single file from one location to another location using relative path on Desktop. This function will create new folders as needed automatically. Both source file and destination file should be file not folder. This function can also be used to rename a file by moving it to a new location with a different file name.")]
    public async Task<string> MoveFile(
        [Description("Source file path, relative file path on Desktop")] string filePath,
        [Description("Destination file path, relative destination path on Desktop")] string destinationPath)
    {
        ReportPluginInUse();

        var userConsent = await GetUserConsentAsync($"We are about to move {filePath} to {destinationPath}, please approve or deny.");
        if (!userConsent)
        {
            return "User declined the file movement, please retry.";
        }

        if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(destinationPath))
        {
            return $"Error moving file {filePath} to {destinationPath}. Please provide a valid file path and destination path.";
        }

        filePath = Path.Combine(_desktopPath, filePath);
        destinationPath = Path.Combine(_desktopPath, destinationPath);

        try
        {
            string? destinationFolder = Path.GetDirectoryName(destinationPath);

            EnsureFolderExists(destinationFolder);

            File.Move(filePath, destinationPath);
            return $"File {filePath} moved to {destinationPath}";
        }
        catch (Exception e)
        {
            return $"Error moving file {filePath} to {destinationPath}. {e.Message}";
        }
    }

    [KernelFunction,
     Description("Move a single file or a folder from one location into another folder using relative path on Desktop. This function will create new folders as needed automatically. If you want to move the entire folder, just pass the source folder path in, don't move all files under it one by one.")]
    public async Task<string> MoveIntoFolder(
        [Description("Source folder path, relative folder path on Desktop")] string fileOrFolderPath,
        [Description("Destination folder path where the file or folder will be moved into, relative destination path on Desktop")] string destinationFolder)
    {
        ReportPluginInUse();

        var userConsent = await GetUserConsentAsync($"We are about to move {fileOrFolderPath} to folder {destinationFolder}, please approve or deny.");
        if (!userConsent)
        {
            return "User declined the file movement, please retry.";
        }

        if (string.IsNullOrEmpty(fileOrFolderPath) || string.IsNullOrEmpty(destinationFolder))
        {
            return $"Error moving folder {fileOrFolderPath} to {destinationFolder}. Please provide a valid folder path and destination path.";
        }

        destinationFolder = Path.Combine(_desktopPath, destinationFolder);

        EnsureFolderExists(destinationFolder);

        try
        {
            string sourcePath = Path.Combine(_desktopPath, fileOrFolderPath);
            string destinationPath = string.Empty;

            if (File.Exists(sourcePath))
            {
                destinationPath = Path.Combine(destinationFolder, Path.GetFileName(sourcePath));
            }
            else if (Directory.Exists(sourcePath))
            {
                destinationPath = Path.Combine(destinationFolder, new DirectoryInfo(sourcePath).Name);
            }

            Directory.Move(sourcePath, destinationPath);
            return $"Successfully Moved {fileOrFolderPath} to {destinationFolder}";
        }
        catch (Exception e)
        {
            return $"Error moving folder {fileOrFolderPath} to {destinationFolder}. {e.Message}";
        }
    }

    [KernelFunction,
     Description("Move multiple files or folders from various locations into a folder using relative path on Desktop. This function will create new folders as needed automatically. If you want to move the entire folder, just pass the source folder path in, don't move all files under it one by one.")]
    public async Task<string> BulkMoveFilesIntoFolder(
        [Description("A list of source files or folders with relative file path on Desktop. If file is in a folder, make sure to include the folder info in the relative path.")] List<string> filePaths,
        [Description("Destination folder path where the file or folder will be moved into, relative destination path on Desktop")] string destinationFolder)
    {
        ReportPluginInUse();

        var files = string.Join("\n", filePaths);
        var userConsent = await GetUserConsentAsync($"We are about to move below files to folder {destinationFolder}, please approve or deny. \n {files}");
        if (!userConsent)
        {
            return "User declined the file movement, please retry.";
        }

        if (filePaths == null || filePaths.Count == 0 || destinationFolder == null || destinationFolder == "")
        {
            return $"Error moving files to {destinationFolder}. Please provide a valid file path and destination path.";
        }

        destinationFolder = Path.Combine(_desktopPath, destinationFolder);

        EnsureFolderExists(destinationFolder);

        List<string> movedFiles = [];
        try
        {
            foreach (var file in filePaths)
            {
                string sourcePath = Path.Combine(_desktopPath, file);
                string destinationPath = string.Empty;
                if (File.Exists(sourcePath))
                {
                    destinationPath = Path.Combine(destinationFolder, Path.GetFileName(sourcePath));
                }
                else if (Directory.Exists(sourcePath))
                {
                    destinationPath = Path.Combine(destinationFolder, new DirectoryInfo(sourcePath).Name);
                }

                Directory.Move(sourcePath, destinationPath);
                movedFiles.Add(file);
            }
            return $"Files moved to {destinationFolder}";

        }
        catch (Exception e)
        {
            return $"Error moving all files to {destinationFolder}. {e.Message}. Moved files are: {string.Join(", ", [.. movedFiles])}";
        }
    }

    [KernelFunction,
     Description("Move all files and folders into a folder")]
    public async Task<string> MoveAllFilesIntoFolder(
        [Description("Destination folder name where the file or folder will be moved into")] string destinationFolder)
    {
        ReportPluginInUse();

        try
        {
            destinationFolder = Path.Combine(_desktopPath, destinationFolder);

            string[] files = Directory.GetFiles(_desktopPath);
            string[] directories = Directory.GetDirectories(_desktopPath);

            if (!Directory.Exists(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
            }

            foreach (string file in files)
            {
                if (file is null or "")
                {
                    continue;
                }
                if (File.Exists(file))
                {
                    string fileName = Path.GetFileName(file);
                    string destFile = Path.Combine(destinationFolder, fileName);
                    File.Move(file, destFile);
                }
            }

            foreach (string directory in directories)
            {
                if (directory is null or "")
                {
                    continue;
                }
                if (Directory.Exists(directory))
                {
                    string dirName = new DirectoryInfo(directory).Name;
                    string destDir = Path.Combine(destinationFolder, dirName);
                    Directory.Move(directory, destDir);
                }
            }

            return $"All files and folders have been moved to {destinationFolder}.";
        }
        catch (Exception ex)
        {
            // Return the exception message if something goes wrong
            return $"An error occurred: {ex.Message}";
        }
    }

    [KernelFunction,
     Description("Delete all empty folders on Desktop.")]
    public async Task<string> DeleteEmptyFolders()
    {
        ReportPluginInUse();

        var userConsent = await GetUserConsentAsync("We are about to delete all empty folders on Desktop. Please approve or deny.");
        if (!userConsent)
        {
            return "User denied the file operations. Please retry or ask something else";
        }

        try
        {
            foreach (var directory in Directory.GetDirectories(_desktopPath, "*", SearchOption.AllDirectories))
            {
                if (Directory.GetFiles(directory).Length == 0 && Directory.GetDirectories(directory).Length == 0)
                {
                    Directory.Delete(directory);
                }
            }
            return "Empty folders deleted successfully.";
        }
        catch (Exception e)
        {
            return $"Error deleting empty folders. {e.Message}";
        }
    }

    [KernelFunction,
     Description("Count files on the desktop")]
    public int CountFiles()
        {
        ReportPluginInUse();

        return Directory.GetFiles(_desktopPath, "*", SearchOption.AllDirectories).Length;
    }

    [KernelFunction,
     Description("Restore Desktop to its original state, and revert all changes.")]
    public async Task<string> RestoreDesktop()
    {
        ReportPluginInUse();

        var userConsent = await GetUserConsentAsync("We are about to restore Desktop to its original state. Please approve or deny.");
        if (!userConsent)
        {
            return "User denied the file operations. Please retry or ask something else";
        }

        string? parentDirectory = Directory.GetParent(_desktopPath)?.FullName;
        if (parentDirectory is not null)
        {
            Directory.SetCurrentDirectory(parentDirectory);
            if (RunCommand("git reset --hard") &&
                RunCommand("git clean -f -d"))
            {
                return "Desktop restored to its original state.";
            }
        }

        return "Error restoring Desktop to its original state.";
    }

    private static void ReportPluginInUse([CallerMemberName] string? functionName = null)
    {
        WeakReferenceMessenger.Default.Send(new PluginInUseMessage(new()
        {
            { "pluginName", "OrganizeDesktopPlugin" },
            { "functionName", functionName ?? "Unknown" }
        }));
    }

    public static void Backup()
    {
        string? parentDirectory = Directory.GetParent(_desktopPath)?.FullName;

        if (parentDirectory is not null)
        {
            Directory.SetCurrentDirectory(parentDirectory);
            if (RunCommand("git add .") &&
                RunCommand("git commit -m 'backup'"))
            {
                App.AlertService?.ShowAlert("Backup Desktop", "Desktop was backed up successfully");
                return;
            }
        }

        App.AlertService?.ShowAlert("Backup Desktop", "Failed to backup desktop, please retry");
    }

    public static bool RunCommand(string command)
    {
        using Process process = Process.Start(new ProcessStartInfo()
        {
            FileName = "cmd.exe",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        })!;

        process.StandardInput.WriteLine(command);
        process.StandardInput.Close();

        _ = process.StandardOutput.ReadToEnd();

        process.WaitForExit();

        return process.ExitCode == 0;
    }

    private static void EnsureFolderExists(string? folderPath)
    {
        if (folderPath is not null && !Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }
    }

    private static Task<bool> GetUserConsentAsync(string message)
    {
        if (App.AlertService is IAlertService service)
        {
            var tcs = new TaskCompletionSource<bool>();
            service.ShowConfirmation("User consent required", message, tcs.SetResult, "Approve", "Deny");
            return tcs.Task;
        }

        return Task.FromResult(false);


    }

    private IEnumerable<string> GetFilesExcludingGitRepos(string path, string searchPattern, SearchOption searchOption)
    {
        var stack = new Stack<string>();
        stack.Push(path);

        while (stack.Count > 0)
        {
            var currentDirectory = stack.Pop();
            if (Directory.Exists(Path.Combine(currentDirectory, ".git")))
            {
                continue;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(currentDirectory, searchPattern);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
            {
                yield return file;
            }

            if (searchOption == SearchOption.AllDirectories)
            {
                string[] directories;
                try
                {
                    directories = Directory.GetDirectories(currentDirectory);
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                foreach (var directory in directories)
                {
                    stack.Push(directory);
                }
            }
        }
    }
}

