using Azure.AI.OpenAI;
using OpenAI.Chat;

public static class AgentTools
{
    // --- 1. WEATHER AGENT (Tahminci Uzman) ---
    public static string ConsultWeatherAgent(string city, ChatClient chatClient)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n[Orchestrator] -> Asking 'Weather Specialist Agent' about {city}...");
        Console.ResetColor();

        // Tarihi dinamik alıyoruz ki Ajan mevsime göre tahmin yapsın.
        string today = DateTime.Now.ToString("MMMM dd");

        var weatherMessages = new List<ChatMessage>()
        {
            new SystemChatMessage($@"
                You are an Expert Meteorologist.
                You do not have access to live sensors, but you have deep knowledge of historical climate patterns.
                
                Task: Estimate the likely weather for {city} based on the current date: {today}.
                Output format must be JSON: {{ ""temperature"": ""XX°C"", ""condition"": ""Sunny/Cloudy/Rainy"", ""note"": ""Brief advice"" }}
                
                Be realistic. If it's November in London, do not say it's 30°C.
            "),
            new UserChatMessage($"What is the weather likely to be in {city} today?")
        };

        // Yapay Zeka, Yapay Zekaya soruyor
        var response = chatClient.CompleteChat(weatherMessages);

        string result = response.Value.Content[0].Text;

        // Gelen cevabı loglayalım (Debug için)
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[Weather Agent]: Prediction: {result}");
        Console.ResetColor();

        return result;
    }

    public static string ConsultCityGuideAgent(string city, ChatClient chatClient)
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"\n[Orchestrator] -> Asking 'City Guide Agent' about {city}...");
        Console.ResetColor();

        var subAgentMessages = new List<ChatMessage>()
        {
            new SystemChatMessage(@"You are a Local City Guide Specialist. 
            You ONLY know about tourist attractions, museums, and food.
            Your output must be a short, bulleted list of top 3 things to do in the requested city.
            Do not talk about weather or hotels."),
            new UserChatMessage($"Tell me the top attractions in {city}.")
        };

        var response = chatClient.CompleteChat(subAgentMessages);

        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"[City Guide Agent]: Advice received.");
        Console.ResetColor();

        return response.Value.Content[0].Text;
    }
}