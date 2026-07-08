using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Poker_With_Your_Friends.Model;
using Poker_With_Your_Friends.ViewModel;
using System;

namespace Poker_With_Your_Friends
{
    public partial class App : Application
    {
        private Window? _window;

        public static MainWindow MainWindowInstance { get; private set; }
        public static Microsoft.UI.Dispatching.DispatcherQueue MainDispatcher { get; set; } = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        public new static App Current => (App)Application.Current;
        public IServiceProvider Services { get; }
        public App()
        {
            InitializeComponent();

            var services = new ServiceCollection();
            services.AddSingleton<IPlayerStore, PlayerStore>();
            services.AddSingleton<Client>();

            services.AddTransient<GameMenuPageViewModel>();
            services.AddTransient<InGamePageViewModel>();

            Services = services.BuildServiceProvider();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            _window.Activate();
        }
    }
}
