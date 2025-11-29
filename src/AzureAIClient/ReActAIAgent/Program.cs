using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using System.Text.Json;
using System.Net.Http.Json;

    public class Program
    {
        // Azure Pricing API için HttpClient
        static readonly HttpClient httpClient = new HttpClient();

        static async Task Main()
        {
            // --- AYARLAR ---
            string AZURE_OPENAI_ENDPOINT = "https://onr-540-8151-resource.cognitiveservices.azure.com/";
        string AZURE_OPENAI_KEY = "";
        string AZURE_OPENAI_DEPLOYMENT_ID = "gpt-4o";

            var endpoint = new Uri(AZURE_OPENAI_ENDPOINT);
            AzureOpenAIClient azureClient = new(endpoint, new AzureKeyCredential(AZURE_OPENAI_KEY));
            ChatClient chatClient = azureClient.GetChatClient(AZURE_OPENAI_DEPLOYMENT_ID);

            // --- 1. TOOL TANIMI: Azure Pricing API ---
            // Ajanın fiyatları sorgulayabilmesi için ona bir "Araç" veriyoruz.
            var priceTool = ChatTool.CreateFunctionTool(
                functionName: "GetAzurePrice",
                functionDescription: "Queries the Azure Retail Prices API to get the monthly cost of a specific resource sku.",
                functionParameters: BinaryData.FromObjectAsJson(new
                {
                    type = "object",
                    properties = new
                    {
                        location = new { type = "string", description = "Azure region, e.g., 'EU West', 'US East'" },
                        serviceName = new { type = "string", description = "The service name, e.g., 'Virtual Machines', 'Storage'" },
                        skuName = new { type = "string", description = "The specific SKU to search for, e.g., 'D2s v3', 'Standard_LRS'" }
                    },
                    required = new[] { "location", "serviceName" }
                })
            );

            List<ChatMessage> messages = new List<ChatMessage>()
            {
                new SystemChatMessage(@"You are a DevOps Engineer and Cloud Architect. 
                Your goal is to generate ready-to-use Terraform code based on user requirements.
                ALWAYS check for the estimated cost of the main resources using the 'GetAzurePrice' tool before generating the code.
                Embed the cost estimation as a comment inside the Terraform code.")
            };

            Console.WriteLine("Cloud Infrastructure Agent Ready! (e.g., 'Create a D2s v3 VM in West Europe')\n--------------------------------------------------------------------------------");

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("You: ");
                string input = Console.ReadLine();
                Console.ResetColor();

                if (string.IsNullOrWhiteSpace(input) || input.ToLower() == "exit") break;

                messages.Add(new UserChatMessage(input));

                // Tool'u seçeneklere ekliyoruz
                var options = new ChatCompletionOptions() { Tools = { priceTool } };

                try
                {
                    // 1. LLM'e sor
                    var completion = chatClient.CompleteChat(messages, options);

                    // 2. Tool Çağrısı Var mı?
                    if (completion.Value.FinishReason == ChatFinishReason.ToolCalls)
                    {
                        messages.Add(new AssistantChatMessage(completion.Value.ToolCalls));

                        foreach (var toolCall in completion.Value.ToolCalls)
                        {
                            if (toolCall.FunctionName == "GetAzurePrice")
                        {
                            using JsonDocument args = JsonDocument.Parse(toolCall.FunctionArguments);
                            string loc = args.RootElement.GetProperty("location").GetString();
                            string svc = args.RootElement.GetProperty("serviceName").GetString();

                            // Bazen SKU null gelebilir, kontrol edelim
                            string sku = args.RootElement.TryGetProperty("skuName", out var s) ? s.GetString() : "";

                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"[Agent]: Querying Azure Pricing API for {svc} ({sku}) in {loc}...");
                            Console.ResetColor();

                            // 1. API ÇAĞRISI YAPILIYOR
                            string priceResult = await GetAzurePriceFromApi(loc, svc, sku);

                            // --- EKLEMEN GEREKEN KISIM BURASI (KANIT) ---
                            Console.ForegroundColor = ConsoleColor.Magenta; // Dikkat çekici bir renk
                            Console.WriteLine($"[API RAW RESPONSE]: {priceResult}");
                            Console.ResetColor();
                            // --------------------------------------------

                            messages.Add(new ToolChatMessage(toolCall.Id, priceResult));
                        }
                    }
                        // Tool cevabını verdikten sonra tekrar LLM'e dön
                        completion = chatClient.CompleteChat(messages);
                    }

                    string response = completion.Value.Content[0].Text;
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"\nAI:\n{response}\n");
                    Console.ResetColor();

                    messages.Add(new AssistantChatMessage(response));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error: " + ex.Message);
                }
            }
        }

    // --- GERÇEK API FONKSİYONU ---
    // Azure Retail Prices API (Public)
    static async Task<string> GetAzurePriceFromApi(string location, string serviceName, string skuName)
    {
        try
        {
            // Bölge ismindeki boşlukları temizle ve küçült (West Europe -> westeurope)
            string cleanLocation = location.ToLower().Replace(" ", "");

            // Filtreyi oluştur
            string filter = $"armRegionName eq '{cleanLocation}' and serviceName eq '{serviceName}'";

            if (!string.IsNullOrEmpty(skuName))
            {
                // Contains yerine tam eşleşme veya daha geniş arama denenebilir ama contains genelde iyidir.
                filter += $" and contains(skuName, '{skuName}')";
            }

            string url = $"https://prices.azure.com/api/retail/prices?currencyCode=USD&$filter={Uri.EscapeDataString(filter)}";

            var response = await httpClient.GetFromJsonAsync<JsonElement>(url);
            var items = response.GetProperty("Items");

            if (items.GetArrayLength() > 0)
            {
                // Fiyat bulundu!
                var firstItem = items[0];
                double unitPrice = firstItem.GetProperty("unitPrice").GetDouble();
                string productName = firstItem.GetProperty("productName").GetString();

                return JsonSerializer.Serialize(new
                {
                    found = true,
                    product = productName,
                    hourlyPrice = unitPrice,
                    monthlyEstimate = unitPrice * 730
                });
            }

            // Fiyat bulunamadıysa AI'a bunu açıkça söyleyelim
            return JsonSerializer.Serialize(new { found = false, error = "No pricing data found for this configuration in Azure API." });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { found = false, error = ex.Message });
        }
    }
}
