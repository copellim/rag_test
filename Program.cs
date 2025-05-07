using System.Collections;
using System.Text;
using bg3_chat;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Microsoft.SemanticKernel.Memory;

#pragma warning disable SKEXP0001, SKEXP0010, SKEXP0050

var apikey = Environment.GetEnvironmentVariable("OpenAI_APIKey")!;
const string modelName = "gpt-4.1-nano";
const string embeddingModelName = "text-embedding-3-small";
const string collectionName = "items";
const string filePath = "Data/BG3 Item Index Cheat Sheet.xlsx";
const float minRelevance = 0.4f;

const string qdrantUrl = "http://qdrant:6333/";
const int qdrantVectorSize = 1536;

var kernel = InitKernel();

var logger = kernel.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("Baldur's Gate 3 Item Search");

var memory = await InitSemanticMemory();

var chatClient = kernel.GetRequiredService<IChatCompletionService>();
var chatHistory = new ChatHistory("You are an AI assistant that helps people find information " +
    "about Baldur's Gate 3 items." +
    "Give answers exclusively retrieving information from the provided data. " +
    "If you don't know the answer, say 'I don't know'.");

var streamingStreamBuilder = new StringBuilder();

while (true)
{
    Console.Write("Question: ");
    var question = Console.ReadLine()!;
    var enhancedQuestion = $"Info about {question} in Baldur's Gate 3";
    var filteredResults = await GetSearchResults(enhancedQuestion);
    logger.LogInformation("Trovati {FilteredResultsCount} risultati sopra la soglia {MinRelevance}",
        filteredResults.Count,
        minRelevance);
    
    var resultsText = GetTextSearchResults(filteredResults);
    var contextToRemove = -1;
    if (!string.IsNullOrWhiteSpace(resultsText))
    {
        contextToRemove = chatHistory.Count;
        chatHistory.AddUserMessage($"Here's the data about the items: {resultsText}");
    }
    
    chatHistory.AddUserMessage(enhancedQuestion);
    Console.ForegroundColor = ConsoleColor.Green;
    streamingStreamBuilder.Clear();
    await foreach (var message in chatClient.GetStreamingChatMessageContentsAsync(chatHistory))
    {
        Console.Write(message);
        streamingStreamBuilder.Append(message);
    }
    chatHistory.AddAssistantMessage(streamingStreamBuilder.ToString());
    if (contextToRemove >= 0) 
        chatHistory.RemoveAt(contextToRemove);

    Console.ResetColor();

    Console.WriteLine();
}

Kernel InitKernel()
{
    var kb = Kernel.CreateBuilder();
    kb.AddOpenAIChatCompletion(modelName, apikey);
    kb.Services.AddLogging(c => c.AddConsole().SetMinimumLevel(LogLevel.Warning));
    kb.Services.ConfigureHttpClientDefaults(c => c.AddStandardResilienceHandler());
    return kb.Build();
}

async Task<ISemanticTextMemory> InitSemanticMemory()
{
    var semanticTextMemory = new MemoryBuilder()
        .WithLoggerFactory(kernel.LoggerFactory)
        .WithMemoryStore(new QdrantMemoryStore(qdrantUrl, qdrantVectorSize))
        .WithOpenAITextEmbeddingGeneration(embeddingModelName, apikey)
        .Build();
    
    var collections = await semanticTextMemory.GetCollectionsAsync();
    if (collections.Contains(collectionName))
    {
        logger.LogInformation("Collezione {CollectionName} già esistente", collectionName);
        return semanticTextMemory;
    }
    
    var extractedText = new SpreadsheetTextExtractor(logger)
        .ExtractText(filePath);
    var chunks = new SemanticMemoryChunker(logger)
        .GenerateChunks(extractedText, SpreadsheetTextExtractor.ItemSeparator);
    
    for (var i = 0; i < chunks.Count; i++)
    {
        var key = chunks.Keys.ElementAt(i);
        if (!chunks.TryGetValue(
            key,
            out var paragraph))
        {
            logger.LogError("Paragrafo {I} non trovato", i);
            continue;
        }
        logger.LogDebug("Salvando paragrafo {I}/{ChunksCount} (lunghezza: {Length})",
            i,
            chunks.Count,
            paragraph.Length);
        
        await semanticTextMemory.SaveInformationAsync(collectionName, paragraph, $"paragraph{i}");
        
        logger.LogDebug("Paragrafo {I} salvato con ID: paragraph{I1}", i, i);
    }
    return semanticTextMemory;
}

async Task<List<MemoryQueryResult>> GetSearchResults(string question)
{
    var memoryQueryResults = new List<MemoryQueryResult>();
    var searchResults = memory.SearchAsync(
        collectionName, 
        question, 
        limit: 5, 
        minRelevanceScore: minRelevance);
    
    await foreach (var result in searchResults)
    {
        logger.LogDebug("Risultato: {MetadataId}, Rilevanza: {ResultRelevance:F4}",
            result.Metadata.Id,
            result.Relevance);
        memoryQueryResults.Add(result);
    }
    return memoryQueryResults;
}

string GetTextSearchResults(List<MemoryQueryResult> filteredResults)
{
    var stringBuilder = new StringBuilder();
    filteredResults
        .Select(r => r.Metadata.Text)
        .ToList()
        .ForEach(result => stringBuilder.AppendLine(result));
    
    return stringBuilder.ToString();
}