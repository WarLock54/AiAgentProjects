using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using System.Text.Json;
using UglyToad.PdfPig; // PDF okuma kütüphanesi

    public class Program
    {
        // --- SENARYO: ARANAN POZİSYON ---
        static string JobDescription = @"
            Role: Senior DevOps Engineer
            Requirements:
            - Minimum 3 years experience with Azure or AWS.
            - Strong knowledge of Terraform (IaC).
            - Experience with CI/CD pipelines (Azure DevOps or GitHub Actions).
            - Docker and Kubernetes experience is a must.
            - Python or C# scripting skills.
        ";

        static async Task Main()
        {
            // --- AYARLAR ---
            string AZURE_OPENAI_ENDPOINT = "https://onr-540-8151-resource.cognitiveservices.azure.com/";
        string AZURE_OPENAI_KEY = "";
        string AZURE_OPENAI_DEPLOYMENT_ID = "gpt-4o";

            var endpoint = new Uri(AZURE_OPENAI_ENDPOINT);
            AzureOpenAIClient azureClient = new(endpoint, new AzureKeyCredential(AZURE_OPENAI_KEY));
            ChatClient chatClient = azureClient.GetChatClient(AZURE_OPENAI_DEPLOYMENT_ID);

            // --- 1. TOOLS (ARAÇLAR) ---

            // Araç 1: Mülakat Daveti Hazırla
            var emailTool = ChatTool.CreateFunctionTool(
                functionName: "DraftInterviewEmail",
                functionDescription: "Drafts an invitation email for suitable candidates.",
                functionParameters: BinaryData.FromObjectAsJson(new
                {
                    type = "object",
                    properties = new
                    {
                        candidateName = new { type = "string" },
                        proposedDate = new { type = "string", description = "Next available Tuesday or Thursday" }
                    },
                    required = new[] { "candidateName" }
                })
            );

            // Araç 2: Takvim Kontrolü (Simüle edilmiş Graph API)
            var calendarTool = ChatTool.CreateFunctionTool(
                functionName: "CheckCalendarAvailability",
                functionDescription: "Checks the HR manager's calendar for available slots.",
                functionParameters: BinaryData.FromObjectAsJson(new
                {
                    type = "object",
                    properties = new
                    {
                        date = new { type = "string", description = "Date to check (YYYY-MM-DD)" }
                    },
                    required = new[] { "date" }
                })
            );

            // --- 2. CV OKUMA (SIMULASYON & GERÇEK) ---
            // Gerçek bir PDF dosyan varsa yolunu buraya yazabilirsin.
            // Yoksa aşağıda senin için sanal bir CV oluşturacağım.
            //string cvText = GetMockCvText();
            string cvText = ReadPdf("C:\\Users\\oatalik\\Downloads\\OnurAtalik_CV.pdf"); // Gerçek dosya için bunu aç

            Console.WriteLine($"--- Analyzing Candidate CV ---\n{cvText.Substring(0, 100)}...\n------------------------------");

            // --- 3. AGENT SYSTEM PROMPT ---
            // Burada "Chain of Thought" (Zincirleme Düşünme) tekniği kullanıyoruz.
            string systemPrompt = $@"
                You are an Expert Technical Recruiter.
                Your task is to evaluate a candidate's Resume against the following Job Description:
                {JobDescription}

                Steps:
                1. Analyze the candidate's skills and experience.
                2. Calculate a match score between 0 and 100.
                3. DECISION LOGIC:
                   - IF Score > 70: You MUST first check calendar availability for next week using 'CheckCalendarAvailability'. 
                     Then, draft an interview email using 'DraftInterviewEmail'.
                   - IF Score <= 70: Just output a polite rejection summary text. Do NOT call any tools.
            ";

            List<ChatMessage> messages = new List<ChatMessage>()
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage($"Here is the candidate's resume content:\n\n{cvText}")
            };

            var options = new ChatCompletionOptions() { Tools = { emailTool, calendarTool } };

            Console.WriteLine("Agent is thinking...");

            // --- 4. AGENT DÖNGÜSÜ ---
            // Agent birden fazla tool çağırabilir (önce takvim, sonra email), bu yüzden döngüdeyiz.
            while (true)
            {
                try
                {
                    var completion = chatClient.CompleteChat(messages, options);

                    // Eğer Tool çağırmak istiyorsa (Yani aday başarılıysa)
                    if (completion.Value.FinishReason == ChatFinishReason.ToolCalls)
                    {
                        messages.Add(new AssistantChatMessage(completion.Value.ToolCalls));

                        foreach (var toolCall in completion.Value.ToolCalls)
                        {
                            string toolResult = "";

                            if (toolCall.FunctionName == "CheckCalendarAvailability")
                            {
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine($"[Agent]: Checking Calendar (Microsoft Graph API)...");
                                toolResult = "Available slots: 10:00 AM, 05:00 PM"; // Simüle edilmiş yanıt
                                Console.ResetColor();
                            }
                            else if (toolCall.FunctionName == "DraftInterviewEmail")
                            {
                                using JsonDocument args = JsonDocument.Parse(toolCall.FunctionArguments);
                                string name = args.RootElement.GetProperty("candidateName").GetString();

                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine($"[Agent]: SUCCESS! Score > 70. Drafting email for {name}...");
                                toolResult = "Email draft created successfully.";
                                Console.ResetColor();
                            }

                            messages.Add(new ToolChatMessage(toolCall.Id, toolResult));
                        }

                        // Tool sonuçlarını gönderip tekrar LLM'i çalıştır (Zinciri tamamla)
                        continue;
                    }

                    // Final Cevap
                    string response = completion.Value.Content[0].Text;
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"\n[Final Decision]:\n{response}\n");
                    Console.ResetColor();
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error: " + ex.Message);
                    break;
                }
            }
            Console.ReadKey();
        }

        // --- YARDIMCI METOTLAR ---

        // 1. PDF Okuma Fonksiyonu (PdfPig kullanarak)
        static string ReadPdf(string path)
        {
            try
            {
                using (var document = PdfDocument.Open(path))
                {
                    string text = "";
                    foreach (var page in document.GetPages())
                    {
                        text += page.Text + " ";
                    }
                    return text;
                }
            }
            catch { return "Error reading PDF"; }
        }

        // 2. Sanal CV (Test etmen için)
        static string GetMockCvText()
        {
            return @"
                Onur Atalık

                Software Engineer
                
                Summary:
                Passionate developer with 4 years of experience in Cloud technologies.
                
                Experience:
                - DevOps Engineer at TechCorp (2021-Present): Managed AWS infrastructure using Terraform. 
                  Built CI/CD pipelines with GitHub Actions. Used Docker for containerization.
                - Junior Developer at SoftSol (2019-2021): Developed C# applications.
                
                Skills:
                AWS, Terraform, Docker, Kubernetes, C#, Python, Git.
            ";
        }
    }
