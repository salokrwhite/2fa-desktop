using System.Windows.Input;
using TwoFactorAuth.Utils;

namespace TwoFactorAuth.ViewModels;

public sealed class AboutViewModel : ViewModelBase
{
    public string Title => "关于";
    public string GitHubRepoUrl => "https://github.com/salokrwhite/2fa-desktop";

    public ICommand OpenGitHubRepoCommand { get; }

    public AboutViewModel()
    {
        OpenGitHubRepoCommand = new RelayCommand(OpenGitHubRepo);
    }

    private void OpenGitHubRepo()
    {
        _ = UrlLauncher.TryOpen(GitHubRepoUrl);
    }
}
