using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Syncfusion.Maui.Toolkit.Hosting;

namespace StudyFlow
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureSyncfusionToolkit()
                .ConfigureMauiHandlers(handlers =>
                {
#if WINDOWS
    				Microsoft.Maui.Controls.Handlers.Items.CollectionViewHandler.Mapper.AppendToMapping("KeyboardAccessibleCollectionView", (handler, view) =>
    				{
    					handler.PlatformView.SingleSelectionFollowsFocus = false;
    				});
#endif
                })
                .ConfigureFonts(fonts =>
                {
                    // Static weights cut from Google Fonts' variable Nunito; license in Resources/Raw/Licenses
                    fonts.AddFont("Nunito-Regular.ttf", "NunitoRegular");
                    fonts.AddFont("Nunito-SemiBold.ttf", "NunitoSemiBold");
                    fonts.AddFont("Nunito-Bold.ttf", "NunitoBold");
                    fonts.AddFont("FluentSystemIcons-Regular.ttf", FluentUI.FontFamily);
                });

#if DEBUG
    		builder.Logging.AddDebug();
    		builder.Services.AddLogging(configure => configure.AddDebug());
#endif

            builder.Services.AddSingleton<SubjectRepository>();
            builder.Services.AddSingleton<TaskRepository>();
            builder.Services.AddSingleton<TagRepository>();
            builder.Services.AddSingleton<MaterialRepository>();
            builder.Services.AddSingleton<NoteRepository>();
            builder.Services.AddSingleton<SeedDataService>();
            builder.Services.AddSingleton<MaterialService>();
            // No overall timeout: video uploads can take long. Short API calls set their own.
            builder.Services.AddSingleton(_ => new HttpClient { Timeout = Timeout.InfiniteTimeSpan });
            builder.Services.AddSingleton<YandexAuthService>();
            builder.Services.AddSingleton<YandexDiskClient>();
            builder.Services.AddSingleton<RemoteDeletionRepository>();
            builder.Services.AddSingleton<CloudBackupService>();
            builder.Services.AddSingleton<ModalErrorHandler>();
            builder.Services.AddSingleton<MainPageModel>();
            builder.Services.AddSingleton<SubjectListPageModel>();
            builder.Services.AddSingleton<NoteListPageModel>();
            builder.Services.AddSingleton<MaterialListPageModel>();

            builder.Services.AddTransientWithShellRoute<SubjectDetailPage, SubjectDetailPageModel>("subject");
            builder.Services.AddTransientWithShellRoute<TaskDetailPage, TaskDetailPageModel>("task");
            builder.Services.AddTransientWithShellRoute<NoteDetailPage, NoteDetailPageModel>("note");
            builder.Services.AddTransientWithShellRoute<SettingsPage, SettingsPageModel>("settings");

            return builder.Build();
        }
    }
}
