using System.Text;
using System.Text.Json;

namespace MauiInsights.Web.Services;

public partial class InsightsService : IInsightsService
{
    private readonly HttpClient httpClient;

    private string? appId;
    
    public InsightsService(IHttpClientFactory httpClientFactory)
    {
        httpClient = httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri("https://api.applicationinsights.io/");
    }

    public async Task<bool> ValidateToken(string appId, string token)
    {
        this.appId = appId;
        
        httpClient.DefaultRequestHeaders.Add("x-api-key", token);

        var url = $"/v1/apps/{appId}/events/$all?$top=5";

        var response = await httpClient.GetAsync(url);

        return response.IsSuccessStatusCode;
    }

    public async Task<List<CountPerDay>> GetErrorsPerDay(int days)
    {
        var query = $"exceptions | where customDimensions.IsCrash != 'true' and timestamp > ago({days}d) | summarize count_sum = sum(itemCount) by bin(timestamp,1d)";
        var queryResult = await GetQueryResult<QueryResult>(query);
        var result = new List<CountPerDay>();

        foreach (var row in queryResult.Tables.First().Rows)
        {
            result.Add(new CountPerDay(DateOnly.FromDateTime( DateTime.Parse(row.First().ToString())), int.Parse(row.Last().ToString())));
        }

        return result;
    }
    
    public async Task<List<CountPerDay>> GetCrashesPerDay(int days)
    {
        var query = $"exceptions | where customDimensions.IsCrash == 'true' and timestamp > ago({days}d) | summarize count_sum = sum(itemCount) by bin(timestamp,1d)";
                              var queryResult = await GetQueryResult<QueryResult>(query);
                              var result = new List<CountPerDay>();
                      
                              foreach (var row in queryResult.Tables.First().Rows)
                              {
                                      result.Add(new CountPerDay(DateOnly.FromDateTime( DateTime.Parse(row.First().ToString())), int.Parse(row.Last().ToString())));
                              }
                      
                              return result;
    }

    public async Task<List<CountPerKey>>  GetErrorsGrouped(int days)
    {
        var query = $"exceptions | where customDimensions.IsCrash != 'true' and timestamp > ago({days}d) | summarize count_sum = sum(itemCount) by problemId";

        var queryResult = await GetQueryResult<QueryResult>(query);

        var result = new List<CountPerKey>();

        foreach (var row in queryResult.Tables.First().Rows)
        {
            result.Add(new CountPerKey(row.First().ToString(), int.Parse(row.Last().ToString())));
        }

        return result;
    }
    
    public async Task<List<CountPerKey>>  GetCrashesGrouped(int days)
    {
        var query = $"exceptions | where customDimensions.IsCrash == 'true' and timestamp > ago({days}d) | summarize count_sum = sum(itemCount) by strcat(problemId, \" - \", outerMessage)";

        var queryResult = await GetQueryResult<QueryResult>(query);

        var result = new List<CountPerKey>();

        foreach (var row in queryResult.Tables.First().Rows)
        {
            result.Add(new CountPerKey(row.First().ToString(), int.Parse(row.Last().ToString())));
        }

        return result;
    }

    public Task<ErrorDetails> GetErrorDetails(string id, int days)
    {
        var query = $"exceptions | where customDimensions.IsCrash != 'true' and timestamp > ago({days}d) and problemId == '{id}'";

        return GetErrorDetails(query);
    }

    public Task<ErrorDetails> GetCrashDetails(string id, int days)
    {
        var query = $"exceptions | where customDimensions.IsCrash == 'true' and timestamp > ago({days}d) and strcat(problemId, \" - \", outerMessage) == '{id}'";

        return GetErrorDetails(query);
    }

    private async Task<ErrorDetails> GetErrorDetails(string query)
    {
        var queryResult = await GetQueryResult<QueryResult>(query);

        var result = new ErrorDetails();
        
        foreach (var row in queryResult.Tables.First().Rows)
        {
            var data = GetData(queryResult, row);

            var crash = new ErrorItem(data);
            result.Items.Add(crash);
        }

        result.AffectedUsersCount = result.Items.Where(x => x.UserId is not null).Select(x => x.UserId).Distinct().Count();
        result.AffectedAppVersions = result.Items.Where(x => x.AppVersion is not null).Select(x => x.AppVersion).Distinct().ToList();
        result.AffectedOperatingSystems = result.Items.Where(x => x.ClientOs is not null).Select(x => x.ClientOs).Distinct().ToList();
        
        return result;
    }

    public async Task<List<EventItem>> GetEventsByUser(string userId, DateTime timestamp)
    {
        var toDate = timestamp.ToUniversalTime().ToString("o");
        var fromDate = timestamp.AddHours(-1).ToUniversalTime().ToString("o");
        
        var eventQuery = $"customEvents| where user_Id == '{userId}' and timestamp between (todatetime('{fromDate}') .. todatetime('{toDate}'))";
        var pageViewsQuery = $"pageViews| where user_Id == '{userId}' and timestamp between (todatetime('{fromDate}') .. todatetime('{toDate}'))";
        var errorsQuery = $"exceptions | where user_Id == '{userId}' and customDimensions.IsCrash == 'true' and timestamp between (todatetime('{fromDate}') .. todatetime('{toDate}'))";
        var crashQuery = $"exceptions | where user_Id == '{userId}' and customDimensions.IsCrash != 'true' and timestamp between (todatetime('{fromDate}') .. todatetime('{toDate}'))";
        
        var pageViewsTask= GetQueryResult<QueryResult>(pageViewsQuery);
        var eventsTask = GetQueryResult<QueryResult>(eventQuery);
        var errorsTask = GetQueryResult<QueryResult>(errorsQuery);
        var crashTask = GetQueryResult<QueryResult>(crashQuery);

        await Task.WhenAll(pageViewsTask, eventsTask, errorsTask, crashTask);
 
        var pageViewsResult = pageViewsTask.Result;
        var eventsResult = eventsTask.Result;
        var errorsResult = errorsTask.Result;
        var crashResult = crashTask.Result;
        
        var result = new List<EventItem>();
        
        foreach (var row in pageViewsResult.Tables.First().Rows)
        {
            var data = GetData(pageViewsResult, row);

            var eventItem = new EventItem(data, EventType.PageView);
            result.Add(eventItem);
        }
        
        foreach (var row in eventsResult.Tables.First().Rows)
        {
            var data = GetData(eventsResult, row);

            var eventItem = new EventItem(data, EventType.CustomEvent);
            result.Add(eventItem);
        }
        
        foreach (var row in errorsResult.Tables.First().Rows)
        {
            var data = GetData(errorsResult, row);

            var eventItem = new EventItem(data, EventType.Error);
            result.Add(eventItem);
        }
        
        foreach (var row in crashResult.Tables.First().Rows)
        {
            var data = GetData(crashResult, row);

            var eventItem = new EventItem(data, EventType.Crash);
            result.Add(eventItem);
        }

        return result.OrderByDescending(x => x.Timestamp).ToList();
    }
    
    private async Task<T> GetQueryResult<T>(string query)
    {
        var url = $"/v1/apps/{appId}/query";

        var json = JsonSerializer.Serialize(new { query = query });
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await httpClient.PostAsync(url, content);

        var responseContent = await response.Content.ReadAsStringAsync();

        var queryResult = JsonSerializer.Deserialize<T>(responseContent, new JsonSerializerOptions() {PropertyNameCaseInsensitive = true});

        return queryResult;
    }

    private Dictionary<string, string?> GetData(QueryResult queryResult, List<object> row)
    {
        var data = new Dictionary<string, string?>();
            
        for (var i = 0; i < row.Count; i++)
        {

            var current = row[i];
            var column = queryResult.Tables.First().Columns[i];

            if (queryResult.Tables.First().Columns[i].Name  == "customDimensions")
            {
                var json = current.ToString();

                var custom = JsonSerializer.Deserialize<CustomDimensions>(json);

                data.Add(nameof(CustomDimensions.AppBuildNumber), custom.AppBuildNumber);
                data.Add(nameof(CustomDimensions.AppVersion), custom.AppVersion);
                data.Add(nameof(CustomDimensions.Language), custom.Language);
                data.Add(nameof(CustomDimensions.StackTrace), custom.StackTrace);
                data.Add(nameof(CustomDimensions.Manufacturer), custom.Manufacturer);
            }
            else
            {
                if (current is JsonElement element)
                {
                    data.Add(column.Name, element.ToString());
                }
                    
            }
        }

        return data;
    }

    public class QueryResult
    {
        public List<Table> Tables { get; set; }
    }

    public class Column
    {
        public string Name { get; set; }
        
        public string Type { get; set; }
    }
    
    public class Table
    {
        public List<Column> Columns { get; set; }
        public List<List<object>> Rows { get; set; }
    }

    public class CustomDimensions
    {
        public string? AppBuildNumber { get; set; }
        public string? AppVersion { get; set; }
        public string? Language { get; set; }
        public string? StackTrace { get; set; }
        public string? Manufacturer { get; set; }
    }
}