using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using WaveDL.Services.Abstractions;

namespace WaveDL.Services;

public sealed class NavigationService : INavigationService
{
    private readonly Dictionary<string, Type> _routes = new(StringComparer.OrdinalIgnoreCase);
    private Frame? _frame;

    public bool CanGoBack => _frame?.CanGoBack ?? false;

    public event EventHandler<string>? Navigated;

    public void Register(string key, Type pageType) => _routes[key] = pageType;

    public void Initialize(Frame frame)
    {
        _frame = frame;
        _frame.Navigated += (_, e) =>
        {
            var key = _routes.FirstOrDefault(kv => kv.Value == e.SourcePageType).Key ?? string.Empty;
            Navigated?.Invoke(this, key);
        };
    }

    public bool Navigate(string key, object? parameter = null)
    {
        if (_frame is null || !_routes.TryGetValue(key, out var pageType))
        {
            return false;
        }

        if (_frame.Content?.GetType() == pageType && parameter is null)
        {
            return false;
        }

        return _frame.Navigate(pageType, parameter, new SlideNavigationTransitionInfo
        {
            Effect = SlideNavigationTransitionEffect.FromRight,
        });
    }

    public void GoBack()
    {
        if (_frame?.CanGoBack == true)
        {
            _frame.GoBack();
        }
    }
}
