

using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using System.Text.Json;

namespace AiAzureClient
{
    public class Program
    {
        static async Task Main()
        {
            // --- 0. VERİTABANI BAŞLAT ---
            DatabaseHelper.InitializeDatabase();
            Console.WriteLine("In-Memory Database Initialized with dummy sales data.");

            // --- 1. AYARLAR ---
            string AZURE_OPENAI_ENDPOINT = "https://onr-540-8151-resource.cognitiveservices.azure.com/";
            string AZURE_OPENAI_KEY = "";
            string AZURE_OPENAI_DEPLOYMENT_ID = "gpt-4o";

            var endpoint = new Uri(AZURE_OPENAI_ENDPOINT);
            AzureOpenAIClient azureClient = new(endpoint, new AzureKeyCredential(AZURE_OPENAI_KEY));
            ChatClient chatClient = azureClient.GetChatClient(AZURE_OPENAI_DEPLOYMENT_ID);

            // --- 2. TOOL TANIMI: RunSQL ---
            var sqlTool = ChatTool.CreateFunctionTool(
                functionName: "RunSql",
                functionDescription: "Executes a SQL query on the 'Sales' database and returns the results.",
                functionParameters: BinaryData.FromObjectAsJson(new
                {
                    type = "object",
                    properties = new
                    {
                        query = new { type = "string", description = "The SQL query to execute. MUST be a SELECT statement." }
                    },
                    required = new[] { "query" }
                })
            );

            // --- 3. SYSTEM PROMPT (KRİTİK BÖLÜM) ---
            // Ajan'a veritabanı yapısını öğretiyoruz (Schema Injection)
            string schemaInfo = @"
                You are a Data Analyst Agent capable of querying a SQL database.
                
                Database Schema:
                Table Name: Sales
                Columns:
                - Id (Integer)
                - ProductName (Text)
                - Category (Text)
                - Quantity (Integer)
                - Price (Real)
                - SaleDate (Text, format YYYY-MM-DD)

                Rules:
                1. Always generate valid SQLite syntax.
                2. Do not use functions that don't exist in SQLite.
                3. When asked for analysis, first Query the data, then explain the result.
            ";

            List<ChatMessage> messages = new List<ChatMessage>()
            {
                new SystemChatMessage(schemaInfo)
            };

            Console.WriteLine("SQL Detective Ready! (e.g., 'What is the total revenue from Electronics?')\n--------------------------------------------------------------------------------");

            // --- 4. ANA DÖNGÜ ---
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("You: ");
                string input = Console.ReadLine();
                Console.ResetColor();

                if (string.IsNullOrWhiteSpace(input) || input.ToLower() == "exit") break;

                messages.Add(new UserChatMessage(input));
                var options = new ChatCompletionOptions() { Tools = { sqlTool } };

                try
                {
                    var completion = chatClient.CompleteChat(messages, options);

                    if (completion.Value.FinishReason == ChatFinishReason.ToolCalls)
                    {
                        messages.Add(new AssistantChatMessage(completion.Value.ToolCalls));

                        foreach (var toolCall in completion.Value.ToolCalls)
                        {
                            if (toolCall.FunctionName == "RunSql")
                            {
                                using JsonDocument args = JsonDocument.Parse(toolCall.FunctionArguments);
                                string sqlQuery = args.RootElement.GetProperty("query").GetString();

                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine($"[Agent Generated SQL]: {sqlQuery}");
                                Console.ResetColor();

                                // SQL'i Çalıştır
                                string queryResult = DatabaseHelper.ExecuteQuery(sqlQuery);

                                Console.ForegroundColor = ConsoleColor.Magenta;
                                Console.WriteLine($"[DB Result]: {queryResult}");
                                Console.ResetColor();

                                messages.Add(new ToolChatMessage(toolCall.Id, queryResult));
                            }
                        }
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
    }
}