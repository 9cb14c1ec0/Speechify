using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace Speechify.Services;

public class ConfigurationService
{
    private readonly IConfiguration _configuration;
    private readonly string _configPath;

    public ConfigurationService()
    {
        _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

        _configuration = builder.Build();
    }

    public string? GetApiKey()
    {
        var apiKey = _configuration["OpenAI:ApiKey"];
        return string.IsNullOrWhiteSpace(apiKey) ? null : apiKey;
    }

    public void SaveApiKey(string apiKey)
    {
        try
        {
            var json = File.ReadAllText(_configPath);
            dynamic jsonObj = Newtonsoft.Json.JsonConvert.DeserializeObject(json) ?? new { };
            
            if (jsonObj.OpenAI == null)
            {
                jsonObj.OpenAI = new { };
            }
            
            jsonObj.OpenAI.ApiKey = apiKey;
            
            string output = Newtonsoft.Json.JsonConvert.SerializeObject(jsonObj, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(_configPath, output);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to save API key: {ex.Message}");
        }
    }
}