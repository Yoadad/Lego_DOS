using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;

namespace WebScrapingAppWithHttpClientFactory
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var services = new ServiceCollection();
            ConfigureServices(services);

            var serviceProvider = services.BuildServiceProvider();
            var webScrapingService = serviceProvider.GetRequiredService<IWebScrapingService>();

            var token = await webScrapingService.GetSecurityTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                Console.WriteLine("Security Token: " + token);

                var loginSuccess = await webScrapingService.LoginAsync(token);
                Console.WriteLine(loginSuccess ? "Login Successful!" : "Login Failed.");

                if (loginSuccess)
                {

                    while (true)
                    {
                        var addAddressSuccess = await webScrapingService.AddAddressAsync(token);
                        Console.WriteLine(addAddressSuccess ? "Address added successfully!" : "Failed to add address.");
                    }
                }
            }
        }

        private static void ConfigureServices(ServiceCollection services)
        {
            // Register HttpClient with HttpClientFactory
            services.AddHttpClient<IWebScrapingService, WebScrapingService>(client =>
            {
                client.DefaultRequestHeaders.Add("Accept", "text/html");
            });

            // Register services
            services.AddScoped<IWebScrapingService, WebScrapingService>();
        }
    }

    public interface IWebScrapingService
    {
        Task<string> GetSecurityTokenAsync();
        Task<bool> LoginAsync(string securityToken);
        Task<bool> AddAddressAsync(string securityToken);
    }

    public class WebScrapingService : IWebScrapingService
    {
        private readonly HttpClient _httpClient;

        public WebScrapingService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> GetSecurityTokenAsync()
        {
            // Use absolute URI for the request to get security token
            var response = await _httpClient.GetAsync("https://www.legocolombia.com.co/index.php?main_page=login&action=process");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var tokenMatch = Regex.Match(content, @"<input\s+type=[""']hidden[""']\s+name=[""']securityToken[""']\s+value=[""']([^""']+)[""']");

                return tokenMatch.Success ? tokenMatch.Groups[1].Value : null;
            }
            return null;
        }

        public async Task<bool> LoginAsync(string securityToken)
        {
            var formData = new MultipartFormDataContent
            {
                { new StringContent("a@b.com"), "email_address" },
                { new StringContent("Aa12345"), "password" },
                { new StringContent(securityToken), "securityToken" }
            };

            // Use absolute URI for login
            var response = await _httpClient.PostAsync("https://www.legocolombia.com.co/index.php?main_page=login&action=process", formData);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> AddAddressAsync(string securityToken)
        {
            var formData = new MultipartFormDataContent
            {
                { new StringContent("你有很多"), "firstname" },
                { new StringContent("紧身女孩"), "lastname" },
                { new StringContent($"The big wall {new String((char)new Random(13).Next(), 55)} "), "street_address" },
                { new StringContent("Beigin"), "city" },
                { new StringContent("12345"), "postcode" },
                { new StringContent("44"), "zone_country_id" },
                { new StringContent("Yes"), "primary" },
                { new StringContent("process"), "action" },
                { new StringContent(securityToken), "securityToken" }
            };

            // Use absolute URI for adding address
            var response = await _httpClient.PostAsync("https://www.legocolombia.com.co/index.php?main_page=address_book_process", formData);
            return response.IsSuccessStatusCode;
        }
    }
}
