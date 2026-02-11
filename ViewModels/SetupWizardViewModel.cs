using System;
using System.Threading.Tasks;
using TwoFactorAuth.Services;

namespace TwoFactorAuth.ViewModels;

public sealed class SetupWizardViewModel : ViewModelBase
{
    private readonly TaskCompletionSource<(string Language, string Theme)> _tcs = new();
    private int _stepIndex;

    public LanguageSelectionViewModel LanguageStep { get; }
    public ThemeSelectionViewModel ThemeStep { get; }

    public int StepIndex
    {
        get => _stepIndex;
        private set
        {
            if (!SetField(ref _stepIndex, value)) return;
            RaisePropertyChanged(nameof(IsBackVisible));
            RaisePropertyChanged(nameof(IsNextVisible));
            RaisePropertyChanged(nameof(IsFinishVisible));
            RaisePropertyChanged(nameof(CurrentStep));
        }
    }

    public ViewModelBase CurrentStep => StepIndex == 0 ? LanguageStep : ThemeStep;

    public bool IsBackVisible => StepIndex > 0;
    public bool IsNextVisible => StepIndex == 0;
    public bool IsFinishVisible => StepIndex == 1;

    public Task<(string Language, string Theme)> Completion => _tcs.Task;

    public SetupWizardViewModel(string defaultLanguage, string defaultTheme)
    {
        AppAppearance.ApplyLanguage(defaultLanguage);
        LanguageStep = new LanguageSelectionViewModel(defaultLanguage, onLanguageChanged: AppAppearance.ApplyLanguage);
        ThemeStep = new ThemeSelectionViewModel(defaultTheme, onThemeChanged: AppAppearance.ApplyTheme);
        StepIndex = 0;
    }

    public void Next()
    {
        if (StepIndex >= 1) return;
        AppAppearance.ApplyLanguage(LanguageStep.SelectedLanguage);
        StepIndex = 1;
    }

    public void Back()
    {
        if (StepIndex <= 0) return;
        StepIndex = 0;
    }

    public void Finish()
    {
        var lang = LanguageStep.SelectedLanguage;
        var theme = ThemeStep.SelectedTheme;
        _tcs.TrySetResult((lang, theme));
    }

    public void Cancel()
    {
        _tcs.TrySetCanceled();
    }
}
