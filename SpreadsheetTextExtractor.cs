using System.Text;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;

namespace bg3_chat;

/// <summary>
/// Helper per l'estrazione di dati da file di foglio di calcolo Excel e la loro conversione in formato testuale.
/// Ottimizzato per l'utilizzo con sistemi RAG (Retrieval-Augmented Generation).
/// </summary>
public class SpreadsheetTextExtractor(
    ILogger logger)
{
    /// <summary>
    /// Foglio di lavoro da escludere durante l'estrazione.
    /// </summary>
    private const string ExcludedWorksheet = "INDEX";
    public static string ItemSeparator => "--------------------";

    /// <summary>
    /// Estrae il testo da un file di foglio di calcolo.
    /// </summary>
    /// <param name="filePath">Percorso del file Excel</param>
    /// <returns>Testo estratto formattato</returns>
    public string ExtractText(string filePath)
    {
        ValidateFilePath(filePath);
            
        try
        {
            using var workbook = new XLWorkbook(filePath);
            var items = ExtractItemsFromWorkbook(workbook)
                .Distinct()
                .ToList();
            return FormatItemsForRag(items);
        }
        catch (Exception ex)
        {
            throw new ExtractionException($"Errore durante l'elaborazione del foglio di calcolo: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Estrae gli item dal workbook Excel.
    /// </summary>
    private List<Item> ExtractItemsFromWorkbook(XLWorkbook workbook)
    {
        var items = new List<Item>();
            
        foreach (var worksheet in workbook.Worksheets.Where(w => w.Name != ExcludedWorksheet))
        {
            try
            {
                items.AddRange(ExtractItemsFromWorksheet(worksheet));
            }
            catch (Exception ex)
            {
                logger.LogError("Errore nell'elaborazione del foglio '{WorksheetName}': {ExMessage}",
                    worksheet.Name,
                    ex.Message);
            }
        }
            
        return items;
    }

    /// <summary>
    /// Estrae gli item da un singolo foglio di lavoro.
    /// </summary>
    private static List<Item> ExtractItemsFromWorksheet(IXLWorksheet worksheet)
    {
        var rows = worksheet.RowsUsed().Skip(1);

        return rows
            .Where(row => !row
                .FirstCell()
                .IsEmpty())
            .Select(row => CreateItemFromRow(
                row,
                worksheet.Name))
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .ToList();
    }

    /// <summary>
    /// Crea un oggetto Item dai dati di una riga.
    /// </summary>
    private static Item CreateItemFromRow(IXLRow row, string sourceName) => 
        new Item(
            TryGetCellValue(row, 1),
            TryGetCellValue(row, 2),
            TryGetCellValue(row, 3),
            TryGetCellValue(row, 4),
            TryGetCellValue(row, 5),
            TryGetCellValue(row, 6),
            TryGetCellValue(row, 7),
            sourceName
        );

    /// <summary>
    /// Tenta di ottenere il valore di una cella, gestendo gli errori.
    /// </summary>
    private static string TryGetCellValue(IXLRow row, int columnIndex)
    {
        try
        {
            var cell = row.Cell(columnIndex);
            return cell.IsEmpty() ? string.Empty : cell.GetString().Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Formatta gli item per l'uso con sistemi RAG.
    /// </summary>
    private static string FormatItemsForRag(List<Item> items)
    {
        if (items.Count is 0)
            return string.Empty;

        var sb = new StringBuilder();
            
        foreach (var item in items)
        {
            sb.AppendLine($"Item: {item.Name}");
            
            var properties = BuildItemProperties(item);

            if (properties.Count is not 0)
                sb.AppendLine(string.Join(". ", properties));
                
            if (!string.IsNullOrWhiteSpace(item.Description))
                sb.AppendLine($"Description: {item.Description}");
            
            sb.AppendLine($"ItemID: {GenerateItemId(item)}");
                
            sb.AppendLine(ItemSeparator);
        }
            
        return sb.ToString();
    }

    private static List<string> BuildItemProperties(
        Item item)
    {
        var properties = new List<string>();
                
        if (!string.IsNullOrWhiteSpace(item.Rarity))
            properties.Add($"Rarity: {item.Rarity}");
                    
        if (!string.IsNullOrWhiteSpace(item.Type))
            properties.Add($"Type: {item.Type}");
                    
        if (!string.IsNullOrWhiteSpace(item.Properties))
            properties.Add($"Properties: {item.Properties}");
                
        var locationParts = new[] { item.ActArea, item.Location }
            .Where(s => !string.IsNullOrWhiteSpace(s));
                    
        var effectiveLocation = string.Join(" - ", locationParts);
        if (!string.IsNullOrWhiteSpace(effectiveLocation))
            properties.Add($"Location: {effectiveLocation}");
                
        if (!string.IsNullOrWhiteSpace(item.Source))
            properties.Add($"Source: {item.Source}");
        return properties;
    }

    /// <summary>
    /// Genera un ID univoco per l'item basato su nome e fonte.
    /// Un identificatore univoco può aiutare per il recupero nei sistemi RAG
    /// </summary>
    private static string GenerateItemId(Item item)
    {
        var baseId = item.Name.Replace(" ", "_").ToLowerInvariant();
            
        if (!string.IsNullOrWhiteSpace(item.Source))
        {
            return $"{item.Source.Replace(" ", "_").ToLowerInvariant()}_{baseId}";
        }
            
        return baseId;
    }

    /// <summary>
    /// Valida il percorso del file.
    /// </summary>
    private static void ValidateFilePath(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentNullException(nameof(filePath), "Il percorso del file non può essere vuoto");
            
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Il file specificato non esiste", filePath);
    }
}

/// <summary>
/// Eccezione specializzata per gli errori di estrazione.
/// </summary>
public class ExtractionException : Exception
{
    public ExtractionException(string message) : base(message) { }
    public ExtractionException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Classe modello per rappresentare un item estratto dal foglio di calcolo.
/// </summary>
public class Item(
    string name,
    string rarity,
    string type,
    string properties,
    string actArea,
    string location,
    string description,
    string source = "") : IEquatable<Item>
{
    public string Name { get; } = name;
    public string Rarity { get; } = rarity;
    public string Type { get; } = type;
    public string Properties { get; } = properties;
    public string ActArea { get; } = actArea;
    public string Location { get; } = location;
    public string Description { get; } = description;
    public string Source { get; } = source;

    public bool Equals(
        Item? other)
    {
        if (other is null) 
            return false;

        return Name.Equals(
                other.Name,
                StringComparison.OrdinalIgnoreCase) &&
            Source.Equals(
                other.Source,
                StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Item);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            Name,
            Source);
    }
}