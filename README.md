# TinyInsights

TinyInsights is a library for tracking and a website for showing insights for your .NET MAUI app. Data will be saved in Application Insights. The website will help you to show the data in a more mobile app-friendly user interface.

You can try out the website here, https://mauiinsights.z6.web.core.windows.net/. No data is saved in the application.

## Add TinyInsights to your app
Install the latest version of the package ***TinyInsights.Maui.AppInsights*** to your app project.

```
dotnet add package TinyInsights.Maui.AppInsights
```

### Installation
In ***MauiProgram.cs***, you should call ***UseTinyInsights*** like below.
```csharp
var builder = MauiApp.CreateBuilder();
builder
  .UseMauiApp<App>()
  .ConfigureFonts(fonts =>
    {
      fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
      fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
     })
  .UseTinyInsights("{YOUR_CONNECTION_STRING}")
```

If you want, you can configure what type of events you want to track.
```csharp
builder.UseMauiInsights("{YOUR_CONNECTION_STRING}",
        (provider) =>
        {
            provider.IsTrackDependencyEnabled = true;
            provider.IsTrackEventsEnabled = true;
            provider.IsTrackErrorsEnabled = true;
            provider.IsTrackPageViewsEnabled = true;
            provider.IsTrackCrashesEnabled = true;           
        });
```

### Track events
Crashes will be tracked automatically if it is enabled, other events you need to track manually. 

To get access to the insights provider that you should use to track events, you should inject the ***IInsights*** interface.
```csharp
private readonly IInsights insights;

public MainViewModel(IInsights insights)
{
  this.insights = insights;
}
```
#### Track page views
```csharp
await insights.TrackPageViewAsync("MainView");
```

#### Track custom events
```csharp
await insights.TrackEventAsync("AddButtonTapped");
```

#### Track exceptions
```csharp
catch (Exception ex)
{
    await insights.TrackErrorAsync(ex);
}
```

#### Track additional data
For all the track methods, you can pass additional data by passing a Dictionary.
```csharp
var data = new Dictionary<string, string>()
          {           
              {"key", "value"},
              {"key2", "value2"}
          };

await insights.TrackPageViewAsync("MainView", data);
```

#### Track dependencies
To automatically track HTTP calls you can use the ***InsightsMessageHandler*** together with the HttpClient.
```csharp
private readonly InsightsMessageHandler insightsMessageHandler;
private readonly HttpClient client;

public MainViewModel(InsightsMessageHandler insightsMessageHandler)
{
  this.insightsMessageHandler = insightsMessageHandler;
  client = new HttpClient(insightsMessageHandler);
}
```

You can also create a DependencyTracker and use it to track dependencies.
```csharp
using var dependency = insights.CreateDependencyTracker("BLOB",blobContainer.Uri.Host, url);
await blob.DownloadToAsync(stream);
```
or if you don't want to wait for the scope of the using.
```csharp
var dependency = insights.CreateDependencyTracker("BLOB",blobContainer.Uri.Host, url);
await blob.DownloadToAsync(stream);
dependency.Dispose();
```

#### UserId
By default a random UserId is generated for each user. If you want to set a specific UserId you can do it like below.
```csharp
�nsights.OverrideAnonymousUserId("MyOwnUserId");
```

To generate a new random UserId you can call the method below.
```csharp
�nsights.GenerateNewAnonymousUserId();
```

## Use with ILogger
If you want, you can also use TinyInsights with the ILogger interface.

In ***MauiProgram.cs***, you should call ***UseTinyInsights*** like below.
```csharp
var builder = MauiApp.CreateBuilder();
builder
  .UseMauiApp<App>()
  .ConfigureFonts(fonts =>
    {
      fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
      fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
     })
  .UseTinyInsightsAsILogger("{YOUR_CONNECTION_STRING}")
```

If you want, you can configure what type of events you want to track.
```csharp
builder.UseTinyInsightsAsILogger("{YOUR_CONNECTION_STRING}",
        (provider) =>
        {
            provider.IsTrackDependencyEnabled = true;
            provider.IsTrackEventsEnabled = true;
            provider.IsTrackErrorsEnabled = true;
            provider.IsTrackPageViewsEnabled = true;
            provider.IsTrackCrashesEnabled = true;           
        });
```

### Use ILogger
To use **ILogger**, just inject the interface in the class you want to use it in.

```csharp
private readonly ILogger logger;

public class MainViewModel(ILogger logger)
{
    this.logger = logger;
}
```

#### Track page views
```csharp
logger.LogTrace("MainView");
```

#### Track event
```csharp
logger.LogInformation("EventButton");
```

#### Track error
```csharp
 logger.LogError(ex, ex.Message);
 ```

#### Track debug info

 **LogDebug** will only work with the debugger attached because it is using **Debug.WriteLine**.

 ```csharp
logger.LogDebug("Debug message");
```