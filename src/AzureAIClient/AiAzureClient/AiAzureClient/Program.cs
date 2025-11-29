using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;

namespace AiAzureClient
{
    public class Program
    {
        static async Task Main()
        {
            string AZURE_OPENAI_ENDPOINT = "https://onr-540-8151-resource.cognitiveservices.azure.com/";
            string AZURE_OPENAI_KEY = "";
            string AZURE_OPENAI_DEPLOYMENT_ID = "gpt-4o";
            var model = "gpt-4o";

            var endpoint = new Uri(AZURE_OPENAI_ENDPOINT);
            AzureOpenAIClient azureClient = new(
    endpoint,
    new AzureKeyCredential(AZURE_OPENAI_KEY));
            ChatClient chatClient = azureClient.GetChatClient(AZURE_OPENAI_DEPLOYMENT_ID);

            Console.WriteLine("Enter your question:");
            var requestOptions = new ChatCompletionOptions()
            {
                MaxOutputTokenCount = 4096,
                Temperature = 1.0f,
                TopP = 1.0f,

            };
            List<ChatMessage> messages = new List<ChatMessage>()
            {
                new SystemChatMessage("You are a helpful assistant.")
            };

            Console.WriteLine("AI Agent Ready! (Type 'exit' to quit)\n------------------------------------------");

            // --- KRİTİK DEĞİŞİKLİK 2: SONSUZ DÖNGÜ ---
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Green; // Kullanıcı rengi
                Console.Write("You: ");
                string question = Console.ReadLine();
                Console.ResetColor();

                // Çıkış kontrolü
                if (string.IsNullOrWhiteSpace(question) || question.ToLower() == "exit")
                {
                    break;
                }

                // Kullanıcı mesajını geçmişe ekle
                messages.Add(new UserChatMessage(question));

                try
                {
                    // API'ye tüm geçmişi (messages listesini) gönderiyoruz
                    var response = chatClient.CompleteChat(messages, requestOptions);
                    string aiResponse = response.Value.Content[0].Text;

                    Console.ForegroundColor = ConsoleColor.Cyan; // AI rengi
                    Console.WriteLine($"\nAI: {aiResponse}\n");
                    Console.ResetColor();

                    // --- KRİTİK DEĞİŞİKLİK 3: CEVABI GEÇMİŞE EKLE ---
                    // AI'nın cevabını da listeye ekliyoruz, böylece bir sonraki soruda ne cevap verdiğini hatırlar.
                    messages.Add(new AssistantChatMessage(aiResponse));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
        }

    }
    }
