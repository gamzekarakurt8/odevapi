using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using odevapi;
using System.Text.Json.Serialization;
using System.Globalization;

public class DataSyncWorker : BackgroundService
{
    private readonly ILogger<DataSyncWorker> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IConfiguration _configuration;
    private string _apiToken;

    private readonly TimeSpan _interval = TimeSpan.FromSeconds(10);

    public DataSyncWorker(
        ILogger<DataSyncWorker> logger,
        IHttpClientFactory httpClientFactory,
        IServiceScopeFactory serviceScopeFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _serviceScopeFactory = serviceScopeFactory;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _apiToken = await GetApiTokenAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("DataSyncWorker running at: {time}", DateTimeOffset.Now);

            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var data = await GetApiDataAsync();
                await WriteDataToDatabaseAsync(context, data);
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task<string> GetApiTokenAsync()
    {
        var tokenUrl = _configuration["ApiSettings:TokenUrl"];
        var clientHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };

        using var httpClient = new HttpClient(clientHandler);
        var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
        {
            Headers = { Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes("apitest:test123"))) },
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        };

        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(responseString);
        return json.RootElement.GetProperty("response").GetProperty("token").GetString();
    }

    private async Task<JsonDocument> GetApiDataAsync()
    {
        var dataUrl = _configuration["ApiSettings:DataUrl"];
        var clientHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        };

        using var httpClient = new HttpClient(clientHandler);
        var request = new HttpRequestMessage(HttpMethod.Patch, dataUrl)
        {
            Headers = { Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiToken) },
            Content = new StringContent("{\"fieldData\": {}, \"script\": \"getData\"}", System.Text.Encoding.UTF8, "application/json")
        };

        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(responseString);
    }

    private async Task WriteDataToDatabaseAsync(ApplicationDbContext context, JsonDocument data)
    {
        try
        {
            // Ýlk olarak, ana JSON nesnesine eriþin
            var rootElement = data.RootElement;

            // "response" alanýna eriþin
            if (rootElement.TryGetProperty("response", out var responseElement) &&
                responseElement.TryGetProperty("scriptResult", out var scriptResultElement))
            {
                // "scriptResult" içeriðini bir dizi olarak iþleyin
                var scriptResultText = scriptResultElement.GetString();

                // "scriptResult" metnini JSON Document olarak çözümleyin
                using (var scriptResultDoc = JsonDocument.Parse(scriptResultText))
                {
                    var arrayElement = scriptResultDoc.RootElement;

                    if (arrayElement.ValueKind == JsonValueKind.Array)
                    {
                        // Mevcut verileri almak için bir liste oluþturun
                        var items = new List<MyDataModel>();

                        foreach (var item in arrayElement.EnumerateArray())
                        {
                            var hesapKodu = item.GetProperty("hesap_kodu").GetString();

                            // Borc deðerini almak ve dönüþtürmek
                            decimal borc = 0; // Varsayýlan deðer
                            if (item.TryGetProperty("borc", out var borcElement) && borcElement.ValueKind == JsonValueKind.Number)
                            {
                                borc = borcElement.GetDecimal();
                            }

                            var dataModel = new MyDataModel
                            {
                                HesapKodu = hesapKodu,
                                ToplamBorc = borc
                            };

                            // `items` listesine ekle
                            items.Add(dataModel);
                        }

                        // Veritabanýnda mevcut kayýtlarý güncelle ve yeni kayýtlarý ekle
                        foreach (var item in items)
                        {
                            var existingRecord = context.Tablo.SingleOrDefault(x => x.HesapKodu == item.HesapKodu);
                            if (existingRecord != null)
                            {
                                existingRecord.ToplamBorc = item.ToplamBorc;
                            }
                            else
                            {
                                context.Tablo.Add(item);
                            }
                        }

                        // Deðiþiklikleri kaydet
                        await context.SaveChangesAsync();
                    }
                    else
                    {
                        _logger.LogError("Expected JSON array but got something else.");
                    }
                }
            }
            else
            {
                _logger.LogError("Response or scriptResult property not found in JSON.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing data to database.");
        }
    }
}
