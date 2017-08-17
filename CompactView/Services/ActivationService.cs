using System;
using System.Collections.Generic;
using System.Linq;  
using System.Threading.Tasks;

using CompactView.Activation;
using CompactView.Helpers;

using Windows.ApplicationModel.Activation;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using CompactView.Data;

namespace CompactView.Services
{
    //For more information on application activation see https://github.com/Microsoft/WindowsTemplateStudio/blob/master/docs/activation.md
    internal class ActivationService
    {
        private readonly App _app;
        private readonly UIElement _shell;
        private readonly Type _defaultNavItem;

        public ActivationService(App app, Type defaultNavItem, UIElement shell = null)
        {
            _app = app;
            _shell = shell ?? new Frame();
            _defaultNavItem = defaultNavItem;
        }

        public async Task ActivateAsync(object activationArgs)
        {
            long iD=-1;
            if (IsInteractive(activationArgs))
            {

                if (((IActivatedEventArgs)activationArgs).Kind == ActivationKind.Protocol)
                {
                    var targetUrl = Uri.UnescapeDataString(((ProtocolActivatedEventArgs)activationArgs).Uri.Query.Substring(1));
                    Uri uri = new Uri(targetUrl);
                    string Name = uri.Host;
                    iD = await WebsiteDataSource.AddNewAsync(Name, uri);
                }

                // Initialize things like registering background task before the app is loaded
                await InitializeAsync();


                // Do not repeat app initialization when the Window already has content,
                // just ensure that the window is active
                if (Window.Current.Content == null)
                {
                    // Create a Frame to act as the navigation context and navigate to the first page
                    Window.Current.Content = _shell;
                    NavigationService.Frame.NavigationFailed += (sender, e) =>
                    {
                        throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
                    };
                    NavigationService.Frame.Navigated += OnFrameNavigated;
                    if (SystemNavigationManager.GetForCurrentView() != null)
                    {
                        SystemNavigationManager.GetForCurrentView().BackRequested += OnAppViewBackButtonRequested;
                    }
                }
            }

            var activationHandler = GetActivationHandlers()
                                                .FirstOrDefault(h => h.CanHandle(activationArgs));

            if (activationHandler != null)
            {
                await activationHandler.HandleAsync(activationArgs);
            }

            if (IsInteractive(activationArgs))
            {
                var defaultHandler = new DefaultLaunchActivationHandler(_defaultNavItem);
                if (defaultHandler.CanHandle(activationArgs))
                {
                    await defaultHandler.HandleAsync(activationArgs);
                }
                else
                {
                    var protocolHandler = new ProtocolActivationHandler(_defaultNavItem,iD);
                    if (protocolHandler.CanHandle(activationArgs))
                    {
                        await protocolHandler.HandleAsync(activationArgs);
                    }
                }

                // Ensure the current window is active
                Window.Current.Activate();

                // Tasks after activation
                await StartupAsync();

                if (iD != -1) //If the app has been launched via protocol
                {
                    await ((Views.WebViewPage)NavigationService.Frame.Content).EnterMiniView();
                }
            }
        }

        private async Task InitializeAsync()
        {
            await ThemeSelectorService.InitializeAsync();
            await Task.CompletedTask;
        }

        private async Task StartupAsync()
        {
            await WhatsNewDisplayService.ShowIfAppropriate();
            await FirstRunDisplayService.ShowIfAppropriate();
            Services.ThemeSelectorService.SetRequestedTheme();
            await Task.CompletedTask;
        }

        private IEnumerable<ActivationHandler> GetActivationHandlers()
        {
            yield return Singleton<SuspendAndResumeService>.Instance;

            yield break;
        }

        private bool IsInteractive(object args)
        {
            return args is IActivatedEventArgs;
        }

        private void OnFrameNavigated(object sender, NavigationEventArgs e)
        {
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = (NavigationService.CanGoBack) ? 
                AppViewBackButtonVisibility.Visible : AppViewBackButtonVisibility.Collapsed;
        }

        private void OnAppViewBackButtonRequested(object sender, BackRequestedEventArgs e)
        {
            if (NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
                e.Handled = true;
            }
        }
    }
}
