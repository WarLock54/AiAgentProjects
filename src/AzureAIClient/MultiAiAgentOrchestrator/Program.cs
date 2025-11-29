using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using System.Text.Json;

class Program
{
    static void Main(string[] args)
    {
        MainAsync().GetAwaiter().GetResult();
    }

    static async Task MainAsync()
    {
        // --- AYARLAR ---
        string AZURE_OPENAI_ENDPOINT = "https://onr-540-8151-resource.cognitiveservices.azure.com/";
        string AZURE_OPENAI_KEY = "";
        string AZURE_OPENAI_DEPLOYMENT_ID = "gpt-4o";

        var endpoint = new Uri(AZURE_OPENAI_ENDPOINT);
        AzureOpenAIClient azureClient = new(endpoint, new AzureKeyCredential(AZURE_OPENAI_KEY));
        ChatClient chatClient = azureClient.GetChatClient(AZURE_OPENAI_DEPLOYMENT_ID);

        // --- TOOL TANIMLAMALARI ---

        // 1. Hava Durumu Aracı
        var weatherTool = ChatTool.CreateFunctionTool(
            functionName: "GetWeather",
            functionDescription: "Gets the weather forecast for a specific city.",
            functionParameters: BinaryData.FromObjectAsJson(new
            {
                type = "object",
                properties = new { city = new { type = "string" } },
                required = new[] { "city" }
            })
        );

        // 2. Şehir Rehberi Aracı (Bu aslında Sub-Agent'ı tetikler)
        var guideTool = ChatTool.CreateFunctionTool(
            functionName: "ConsultCityGuide",
            functionDescription: "Ask the Specialist City Guide about top attractions in a city.",
            functionParameters: BinaryData.FromObjectAsJson(new
            {
                type = "object",
                properties = new { city = new { type = "string" } },
                required = new[] { "city" }
            })
        );

        // --- ORCHESTRATOR SYSTEM PROMPT ---
        string managerPrompt = @"
                You are the 'Lead Travel Planner' (Orchestrator).
                Your job is to plan a trip for the user.
                
                You have a team of specialists:
                1. Weather Tool: For checking weather conditions.
                2. City Guide Specialist: For finding places to visit.

                Strategy:
                - Always check the weather first to decide if the trip is feasible.
                - Then consult the City Guide to find activities.
                - Finally, combine all information and present a full plan to the user.
            ";

        List<ChatMessage> messages = new List<ChatMessage>()
            {
                new SystemChatMessage(managerPrompt)
            };

        Console.WriteLine("Multi-Agent Travel Planner Ready! (e.g. 'Plan a trip to Rome')\n-----------------------------------------------------------");

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("User: ");
            string input = Console.ReadLine();
            Console.ResetColor();

            if (string.IsNullOrWhiteSpace(input) || input.ToLower() == "exit") break;

            messages.Add(new UserChatMessage(input));

            // Yöneticiye araçları veriyoruz
            var options = new ChatCompletionOptions() { Tools = { weatherTool, guideTool } };

            bool processing = true;
            while (processing)
            {
                // Yönetici Düşünüyor...
                var completion = chatClient.CompleteChat(messages, options);

                if (completion.Value.FinishReason == ChatFinishReason.ToolCalls)
                {
                    // Tool çağrısı isteği geldi, Yönetici bir uzmana ihtiyaç duyuyor.
                    messages.Add(new AssistantChatMessage(completion.Value.ToolCalls));

                    foreach (var toolCall in completion.Value.ToolCalls)
                    {
                        string result = "";

                        if (toolCall.FunctionName == "GetWeather")
                        {
                            Console.WriteLine($"[Orchestrator] -> Calling Weather Tool for ");
                            using JsonDocument args = JsonDocument.Parse(toolCall.FunctionArguments);
                            string city = args.RootElement.GetProperty("city").GetString();
                            // ARTIK OPENMETEO YOK, AI VAR
                            // ConsultWeatherAgent metoduna 'chatClient' parametresini de gönderiyoruz.
                            result = AgentTools.ConsultWeatherAgent(city, chatClient);
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.ResetColor();
                        }
                        else if (toolCall.FunctionName == "ConsultCityGuide")
                        {
                            using JsonDocument args = JsonDocument.Parse(toolCall.FunctionArguments);
                            string city = args.RootElement.GetProperty("city").GetString();

                            // Burada Sub-Agent'ı (Diğer LLM çağrısını) yapıyoruz
                            result = AgentTools.ConsultCityGuideAgent(city, chatClient);
                        }

                        messages.Add(new ToolChatMessage(toolCall.Id, result));
                    }
                }
                else
                {
                    // Yönetici tüm bilgileri topladı, final cevabı veriyor.
                    string finalResponse = completion.Value.Content[0].Text;
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"\n[Lead Planner]:\n{finalResponse}\n");
                    Console.ResetColor();

                    messages.Add(new AssistantChatMessage(finalResponse));
                    processing = false; // Tur tamamlandı
                }
            }
        }
    }
}