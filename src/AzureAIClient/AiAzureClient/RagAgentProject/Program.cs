// --- VERİ MODELİ ---
using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using OpenAI.Embeddings;
using System.Numerics.Tensors;

public class DocItem
{
    public string Id { get; set; }
    public string Content { get; set; } // Metin içeriği
    public ReadOnlyMemory<float> Vector { get; set; } // Sayısal karşılığı (Embedding)
}

public class Program
{
    static async Task Main()
    {
        // --- 1. AYARLAR ---
        string AZURE_OPENAI_ENDPOINT = "https://onr-540-8151-resource.cognitiveservices.azure.com/";
        string AZURE_OPENAI_KEY = "";
        // DİKKAT: Buraya Chat modelini değil, EMBEDDING model ismini yazmalısın.
        string EMBEDDING_DEPLOYMENT = "text-embedding-ada-002";
        string CHAT_DEPLOYMENT = "gpt-4o";

        var endpoint = new Uri(AZURE_OPENAI_ENDPOINT);
        AzureOpenAIClient azureClient = new(endpoint, new AzureKeyCredential(AZURE_OPENAI_KEY));

        // İki farklı istemciye ihtiyacımız var: Biri Embeddings, biri Chat için.
        EmbeddingClient embeddingClient = azureClient.GetEmbeddingClient(EMBEDDING_DEPLOYMENT);
        ChatClient chatClient = azureClient.GetChatClient(CHAT_DEPLOYMENT);

        // --- 2. BİLGİ BANKASI (Knowledge Base) ---
        // Gerçek projede bunlar PDF'ten okunur. Biz simüle ediyoruz.
        var rawDocs = new List<(string Id, string Text)>
            {
                ("DOC-001", "VPN Bağlantı Hatası 691: Bu hata genellikle yanlış kullanıcı adı veya şifre girildiğinde oluşur. Lütfen Active Directory şifrenizi kontrol edin."),
                ("DOC-002", "Yıllık İzin Politikası: Çalışanlar ilk yıl 14 gün, 5. yıldan sonra 20 gün izin hakkına sahiptir. İzin talepleri HR portalından 2 hafta önce girilmelidir."),
                ("DOC-003", "Yazıcı Kurulumu: 3. Katta bulunan 'Canon_Main' yazıcısı için IP adresi 192.168.1.50'dir. Driver sürücüsü Z: sürücüsünde mevcuttur."),
                ("DOC-004", "Proje Yönetimi: Şirketimizde Agile metodolojisi kullanılır. Sprintler 2 hafta sürer ve her sabah 09:30'da Daily Scrum yapılır.")
            };

        Console.WriteLine("--- Building Vector Database (Indexing)... ---");
        List<DocItem> vectorDb = new();

        // --- 3. INDEXING (Metni Sayıya Çevirme) ---
        foreach (var doc in rawDocs)
        {
            // Azure'a metni gönderip vektörünü alıyoruz
            OpenAIEmbedding embedding = await embeddingClient.GenerateEmbeddingAsync(doc.Text);

            vectorDb.Add(new DocItem
            {
                Id = doc.Id,
                Content = doc.Text,
                Vector = embedding.ToFloats()
            });
            Console.WriteLine($"Indexed: {doc.Id}");
        }

        Console.WriteLine("\nKnowledge Base Ready! Ask me about VPN, Holidays, Printers etc.\n-------------------------------------------------------------");

        // --- 4. SOHBET DÖNGÜSÜ ---
        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("Employee: ");
            string query = Console.ReadLine();
            Console.ResetColor();

            if (string.IsNullOrWhiteSpace(query) || query.ToLower() == "exit") break;

            try
            {
                // A. KULLANICI SORUSUNU VEKTÖRE ÇEVİR
                OpenAIEmbedding queryEmbedding = await embeddingClient.GenerateEmbeddingAsync(query);
                ReadOnlyMemory<float> queryVector = queryEmbedding.ToFloats();

                // B. EN BENZER DÖKÜMANI BUL (Cosine Similarity)
                // Her dökümanın soruyla olan benzerlik skorunu hesaplıyoruz.
                var searchResults = vectorDb
                    .Select(doc => new
                    {
                        Doc = doc,
                        Score = TensorPrimitives.CosineSimilarity(queryVector.Span, doc.Vector.Span)
                    })
                    .OrderByDescending(x => x.Score) // En yüksek skor en üstte
                    .Take(2) // En alakalı ilk 2 dökümanı al (Top K)
                    .ToList();

                // C. GROUNDING (Kaynak Gösterme & Güvenlik)
                // Eğer en iyi eşleşme bile çok düşükse (%70'in altı), bilgi yok demektir.
                if (searchResults.First().Score < 0.70f)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("[System]: No relevant information found in the knowledge base. (GAP DETECTED)");
                    Console.ResetColor();
                    continue;
                }

                // Bulunan bilgileri tek bir metin haline getir (Context)
                string contextData = string.Join("\n\n", searchResults.Select(x => $"[Source: {x.Doc.Id}]: {x.Doc.Content}"));

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"[Debug - Retrieved Context]:\n{contextData}\n");
                Console.ResetColor();

                // D. RAG PROMPT HAZIRLA
                string ragPrompt = $@"
    Sen şirket çalışanlarına yardımcı olan bir Teknik Destek Asistanısın.
    Görevin: Kullanıcının sorusunu, aşağıda sana verilen 'Context' (Bağlam) içindeki bilgileri yorumlayarak cevaplamak.

    Kurallar:
    1. Context içindeki metin, sorunun tam cevabı olmasa bile, ilgiliyse o bilgiyi kullan. 
       (Örnek: Kullanıcı 'hangi yazıcı' derse ve context'te 'yazıcı kurulumu' varsa, o yazıcıyı öner.)
    2. Cevabını verirken kaynağı referans göster (Örn: [Source: DOC-001]).
    3. Eğer Context içinde konuyla ALAKALI HİÇBİR ŞEY yoksa, sadece o zaman 'Bilgi bankasında bulunamadı' de.

    Context:
    {contextData}
";

                List<ChatMessage> messages = new()
                    {
                        new SystemChatMessage(ragPrompt),
                        new UserChatMessage(query)
                    };

                var response = chatClient.CompleteChat(messages);

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"AI Support: {response.Value.Content[0].Text}\n");
                Console.ResetColor();

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}