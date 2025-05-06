using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Text;

namespace bg3_chat
{
    /// <summary>
    /// Helper dedicato alla preparazione di chunk semantici per sistemi RAG
    /// a partire da dati estratti da fogli di calcolo.
    /// </summary>
    public class SemanticMemoryChunker(ILogger logger)
    {
        private const int MaxChunkSize = 1024;
        private const int MaxTokensPerLine = 128;
        
        /// <summary>
        /// Processa il testo estratto e genera un dizionario di chunk con relativi ID.
        /// </summary>
        /// <param name="extractedText">Testo già estratto da elaborare</param>
        /// <param name="chunkSeparator">La stringa utilizzata come separatore dei campi</param>
        /// <returns>Dizionario con chunkId come chiave e chunk come valore</returns>
        [Experimental("SKEXP0050")]
        public Dictionary<string, string> GenerateChunks(string extractedText, string chunkSeparator)
        {
            logger.LogInformation("Inizializzazione del processo di chunking (lunghezza testo: {ExtractedTextLength} caratteri)",
                extractedText.Length);
            
            var result = new Dictionary<string, string>();

            var itemChunks = extractedText.Split(chunkSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(chunk => chunk.Trim())
                .Where(chunk => !string.IsNullOrWhiteSpace(chunk))
                .ToList();
            
            logger.LogInformation("Estratti {ItemChunksCount} item da elaborare", itemChunks.Count);
            
            // Elabora ogni item individualmente
            for (var i = 0; i < itemChunks.Count; i++)
            {
                var chunk = itemChunks[i];
                
                // Se il chunk è troppo grande, dividiamolo ulteriormente
                if (chunk.Length > MaxChunkSize)
                {
                    result = result
                        .Concat(
                            ProcessLargeItem(chunk, i, itemChunks.Count))
                        .ToDictionary();
                }
                else
                {
                    result = result
                        .Concat(
                            ProcessSingleItem(chunk, i, itemChunks.Count))
                        .ToDictionary();
                }
            }

            logger.LogInformation("Generati {ResultCount} chunk totali", result.Count);
            return result;
        }

        /// <summary>
        /// Elabora un item di grandi dimensioni dividendolo in sotto-chunk.
        /// </summary>
        [Experimental("SKEXP0050")]
        private Dictionary<string, string> ProcessLargeItem(
            string chunk, 
            int itemIndex, 
            int totalItems)
        {
            var result = new Dictionary<string, string>();
            var subChunks = ChunkItemText(chunk);
            
            for (var j = 0; j < subChunks.Count; j++)
            {
                var itemId = ExtractItemId(chunk);
                var chunkId = string.IsNullOrEmpty(itemId) ? 
                    $"item{itemIndex}_part{j}" : $"{itemId}_part{j}";
                
                logger.LogDebug("Generando chunk {ChunkIndex}/{TotalChunks} dell'item {ItemIndex}/{TotalItems} " +
                    "(lunghezza: {ChunkLenght})", j+1, subChunks.Count, itemIndex+1, totalItems, subChunks[j].Length);
                
                result.Add(chunkId, subChunks[j]);
                
                logger.LogDebug("Sotto-chunk generato con ID: {ChunkId}",
                    chunkId);
            }
            return result;
        }

        /// <summary>
        /// Elabora un singolo item di dimensioni adeguate.
        /// </summary>
        private Dictionary<string, string> ProcessSingleItem(
            string chunk, 
            int itemIndex, 
            int totalItems)
        {
            var result = new Dictionary<string, string>();
            
            var itemId = ExtractItemId(chunk);
            var chunkId = string.IsNullOrEmpty(itemId) ? $"item{itemIndex}" : itemId;
            
            logger.LogDebug("Generando item {ItemIndex}/{TotalItems} (lunghezza: {ChunkLength})",
                itemIndex+1,
                totalItems,
                chunk.Length);
            
            result.Add(chunkId, chunk);
            
            logger.LogDebug("Item generato con ID: {ChunkId}", chunkId);
            return result;
        }

        /// <summary>
        /// Divide un singolo item in chunk più piccoli mantenendo l'integrità semantica.
        /// </summary>
        [Experimental("SKEXP0050")]
        private List<string> ChunkItemText(string itemText)
        {
            var itemName = ExtractItemName(itemText);
            var itemId = ExtractItemId(itemText);
            
            var header = string.IsNullOrEmpty(itemName) ? "" : $"Item: {itemName}\n";
            var lines = TextChunker.SplitPlainTextLines(itemText, MaxTokensPerLine);
            
            // Escludo le linee di intestazione che abbiamo già gestito
            ExcludeAlreadyManagedLines(lines);

            var paragraphs = TextChunker.SplitPlainTextParagraphs(
                lines, 
                MaxTokensPerLine - header.Length);
            
            AddIdAndHeaderToParagraphs(paragraphs,
                header,
                itemId);
            
            return paragraphs;
        }

        private static void ExcludeAlreadyManagedLines(
            List<string> lines)
        {
            if (lines.Count > 0 && lines[0].StartsWith("Item:"))
            {
                lines.RemoveAt(0);
            }

            if (lines.Count > 0 && lines[lines.Count - 1].StartsWith("ItemID:"))
            {
                lines.RemoveAt(lines.Count - 1);
            }
        }

        private static void AddIdAndHeaderToParagraphs(
            List<string> paragraphs,
            string header,
            string itemId)
        {
            for (var i = 0; i < paragraphs.Count; i++)
            {
                paragraphs[i] = header + paragraphs[i];
                if (!string.IsNullOrEmpty(itemId))
                {
                    paragraphs[i] += $"\nItemID: {itemId}";
                }
            }
        }

        /// <summary>
        /// Estrae il nome dell'item dal testo.
        /// </summary>
        private string ExtractItemName(string text)
        {
            // Cerca la riga che inizia con "Item: "
            var lines = text.Split('\n');
            foreach (var line in lines)
            {
                if (line.StartsWith("Item:"))
                {
                    return line.Substring("Item:".Length).Trim();
                }
            }
            return string.Empty;
        }

        /// <summary>
        /// Estrae l'ID dell'item dal testo.
        /// </summary>
        private static string ExtractItemId(string text)
        {
            var lines = text.Split('\n');
            foreach (var line in lines)
            {
                if (line.StartsWith("ItemID:"))
                {
                    return line.Substring("ItemID:".Length).Trim();
                }
            }
            return string.Empty;
        }
    }
}