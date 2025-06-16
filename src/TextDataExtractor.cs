using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

public static class TextDataExtractor
{
    // HashSet for tracking already processed strings (duplicate prevention)
    private static readonly HashSet<string> _processedStrings = new HashSet<string>();
    
    /// <summary>
    /// Extracts all text data to text folder in plugin directory in CSV format
    /// </summary>
    public static void ExtractAllTextToPluginDirectory()
    {
        // Get path to plugin directory
        string pluginDirectory = GetPluginDirectory();
        string textDirectory = Path.Combine(pluginDirectory, "text");
        
        // Create text folder if it doesn't exist
        if (!Directory.Exists(textDirectory))
        {
            Directory.CreateDirectory(textDirectory);
            Debug.Log($"Created text directory: {textDirectory}");
        }

        Debug.Log($"Extracting all text data to CSV files in: {textDirectory}");

        try
        {
                    // Clear HashSet before starting extraction
        _processedStrings.Clear();
        
        // Extract different types of texts into separate CSV files
            ExtractSceneDataToFile(textDirectory);
            ExtractStringListsToFile(textDirectory);
            ExtractItemsToFile(textDirectory);
            ExtractCharactersToFile(textDirectory);
            ExtractQuestsToFile(textDirectory);
            ExtractJournalToFile(textDirectory);
            ExtractBooksToFile(textDirectory);
            ExtractAbilitiesAndSpellsToFile(textDirectory);

            Debug.Log($"✓ All text data successfully extracted to CSV files! Processed {_processedStrings.Count} unique strings.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error during text extraction: {ex.Message}");
            Debug.LogError($"Stack trace: {ex.StackTrace}");
        }
    }

    public static List<string> ExpandConditionals(string input)
    {
        // Удаляем тех. примечания, например ;;Scene: ... и т.д.
        input = Regex.Replace(input, @";;.*?$", "", RegexOptions.Multiline).Trim();

        var outputs = new List<string>();

        // Поиск #IF блоков с THEN и/или ELSE
        var match = Regex.Match(input, @"#IF\s*\(([^)]*)\)\s*#THEN\((.*?)\)(?:#ELSE\((.*?)\))?#END", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (!match.Success)
        {
            outputs.Add(input.Trim());
            return outputs;
        }

        string fullMatch = match.Value;
        string thenPart = match.Groups[2].Value.Trim();
        string elsePart = match.Groups[3].Success ? match.Groups[3].Value.Trim() : "";

        // Удалить внешние кавычки только если вся ветвь в одних кавычках
        thenPart = StripQuotesIfWrapped(thenPart);
        elsePart = StripQuotesIfWrapped(elsePart);

        // Подставляем варианты и продолжаем рекурсию
        outputs.AddRange(ExpandConditionals(input.Replace(fullMatch, thenPart)));
        if (match.Groups[3].Success)
            outputs.AddRange(ExpandConditionals(input.Replace(fullMatch, elsePart)));

        return outputs;
    }

    // Удаляет внешние кавычки, если они парные и не содержат вложенных
    private static string StripQuotesIfWrapped(string s)
    {
        if (s.StartsWith("\"") && s.EndsWith("\"") && s.Count(c => c == '"') == 2)
            return s.Substring(1, s.Length - 2).Trim();
        return s;
    }

    /// <summary>
    /// Extracts text variants from IF-THEN-ELSE constructs
    /// </summary>
    private static List<string> ExtractIfThenElseVariants(string input)
    {
        var result = new List<string>();
        
        // Find IF-THEN-ELSE constructs manually to handle nested braces and parentheses properly
        int ifIndex = input.IndexOf("#IF(");
        while (ifIndex != -1)
        {
            // Find the matching #END
            int endIndex = input.IndexOf("#END", ifIndex);
            if (endIndex == -1) break;
            
            string construct = input.Substring(ifIndex, endIndex - ifIndex + 4);
            
            // Extract THEN part
            int thenIndex = construct.IndexOf("#THEN(");
            if (thenIndex != -1)
            {
                int thenStart = thenIndex + 6; // Skip "#THEN("
                int thenEnd = FindMatchingParenthesis(construct, thenStart - 1);
                if (thenEnd != -1)
                {
                    string thenText = construct.Substring(thenStart, thenEnd - thenStart).Trim();
                    if (!string.IsNullOrEmpty(thenText))
                    {
                        result.Add(Clean(thenText));
                    }
                }
            }
            
            // Extract ELSE part if present
            int elseIndex = construct.IndexOf("#ELSE(");
            if (elseIndex != -1)
            {
                int elseStart = elseIndex + 6; // Skip "#ELSE("
                int elseEnd = FindMatchingParenthesis(construct, elseStart - 1);
                if (elseEnd != -1)
                {
                    string elseText = construct.Substring(elseStart, elseEnd - elseStart).Trim();
                    if (!string.IsNullOrEmpty(elseText))
                    {
                        result.Add(Clean(elseText));
                    }
                }
            }
            
            // Look for next IF construct
            ifIndex = input.IndexOf("#IF(", endIndex);
        }
        
        return result;
    }
    
    /// <summary>
    /// Finds the matching closing parenthesis for an opening parenthesis at the given position
    /// </summary>
    private static int FindMatchingParenthesis(string text, int openIndex)
    {
        if (openIndex >= text.Length || text[openIndex] != '(')
            return -1;
            
        int count = 1;
        for (int i = openIndex + 1; i < text.Length; i++)
        {
            if (text[i] == '(')
                count++;
            else if (text[i] == ')')
            {
                count--;
                if (count == 0)
                    return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Extracts quoted text and bracketed actions as separate elements
    /// </summary>
    private static List<string> ExtractQuotesAndActions(string input)
    {
        var result = new List<string>();
        
        // Check if input contains quotes or brackets that need special handling
        if (!input.Contains("\"") && !input.Contains("["))
            return result;
        
        string remaining = input;
        
        // Extract bracketed actions first [Action text]
        var bracketMatches = Regex.Matches(remaining, @"\[([^\]]+)\]");
        foreach (Match match in bracketMatches)
        {
            string actionText = match.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(actionText))
            {
                result.Add(Clean(actionText));
            }
            // Remove the bracketed action from remaining text
            remaining = remaining.Replace(match.Value, " ");
        }
        
        // Extract quoted text - separate outer text from inner quoted names/phrases
        // Handle patterns like: "Call out to ""Joran the Usurper""
        var complexQuotePattern = @"""([^""]*?)""""([^""]+)""""([^""]*?)""";
        var complexMatches = Regex.Matches(remaining, complexQuotePattern);
        
        foreach (Match match in complexMatches)
        {
            // Extract the three parts: before inner quote, inner quote, after inner quote
            string beforeQuote = match.Groups[1].Value.Trim();
            string innerQuote = match.Groups[2].Value.Trim();
            string afterQuote = match.Groups[3].Value.Trim();
            
            // Add outer text (before + after, combined if both exist)
            var outerParts = new List<string>();
            if (!string.IsNullOrEmpty(beforeQuote))
                outerParts.Add(beforeQuote);
            if (!string.IsNullOrEmpty(afterQuote))
                outerParts.Add(afterQuote);
            
            if (outerParts.Count > 0)
            {
                string outerText = string.Join(" ", outerParts).Trim();
                if (!string.IsNullOrEmpty(outerText))
                    result.Add(Clean(outerText));
            }
            
            // Add inner quoted text as separate string
            if (!string.IsNullOrEmpty(innerQuote))
            {
                result.Add(Clean(innerQuote));
            }
            
            // Remove the processed quote from remaining text
            remaining = remaining.Replace(match.Value, " ");
        }
        
        // Handle simple quotes without inner quotes
        var simpleQuotePattern = @"""([^""]+)""";
        var simpleMatches = Regex.Matches(remaining, simpleQuotePattern);
        
        foreach (Match match in simpleMatches)
        {
            string quotedText = match.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(quotedText))
            {
                result.Add(Clean(quotedText));
            }
            // Remove the quoted text from remaining
            remaining = remaining.Replace(match.Value, " ");
        }
        
        // Process any remaining text that's not quoted or bracketed
        remaining = Regex.Replace(remaining, @"\s+", " ").Trim();
        if (!string.IsNullOrEmpty(remaining))
        {
            // Split remaining text by sentences if it contains sentence endings
            if (Regex.IsMatch(remaining, @"[\.!?]"))
            {
                string[] sentences = Regex.Split(remaining, @"(?<=[\.!?]['""]?)\s*(?=[""']?[A-ZА-Я])");
                foreach (var sentence in sentences)
                {
                    string cleaned = Clean(sentence);
                    if (!string.IsNullOrEmpty(cleaned))
                        result.Add(cleaned);
                }
            }
            else
            {
                string cleaned = Clean(remaining);
                if (!string.IsNullOrEmpty(cleaned))
                    result.Add(cleaned);
            }
        }
        
        return result;
    }

    // Удаляет лишние символы по краям
    private static string Clean(string line)
    {
        return Regex.Replace(line, @"^[\s""'\*]+|[\s""'\*]+$", "").Trim();
    }

    /// <summary>
    /// Creates CSV string in Original,Translate,Comment format
    /// </summary>
    private static string CreateCSVLine(string original, string comment)
    {
        // Экранируем кавычки и точки с запятой
        original = EscapeCSV(original);
        comment = EscapeCSV(comment);
        
        return $"{original};;{comment}";
    }

    /// <summary>
    /// Escapes special characters for CSV
    /// </summary>
    private static string EscapeCSV(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        // Заменяем переносы строк на пробелы
        text = text.Replace("\n", " ").Replace("\r", " ");
        
        // Если содержит кавычки, точки с запятой или переносы - заключаем в кавычки
        if (text.Contains("\"") || text.Contains(";") || text.Contains("\n"))
        {
            text = text.Replace("\"", "\"\""); // Удваиваем кавычки
            text = $"\"{text}\"";
        }

        return text;
    }

    /// <summary>
    /// Adds text to CSV, breaking into sentences and preventing duplicates
    /// </summary>
    private static void AddTextToCSV(StringBuilder csv, string text, string comment)
    {
        if (string.IsNullOrEmpty(text)) return;

        var sentences = GameTextParser.Parse(text);
        foreach (var sentence in sentences)
        {
            // Проверяем, не была ли эта строка уже обработана
            if (!_processedStrings.Contains(sentence))
            {
                _processedStrings.Add(sentence);
                csv.AppendLine(CreateCSVLine(sentence, comment));
            }
        }
    }

    /// <summary>
    /// Extracts scenes and dialogues
    /// </summary>
    private static void ExtractSceneDataToFile(string directory)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Original;Translate;Comment");

        try
        {
            var projectStack = GetProjectStack();
            foreach (var project in projectStack)
            {
                if (project?.data?.sceneData?.list != null)
                {
                    foreach (var sceneContainer in project.data.sceneData.list)
                    {
                        if (sceneContainer?.list != null)
                        {
                            foreach (var nodeContainer in sceneContainer.list)
                            {
                                string sceneSource = nodeContainer.id;
                                
                                if (nodeContainer?.list != null)
                                {
                                    foreach (var sceneNode in nodeContainer.list)
                                    {
                                        string nodeId = sceneNode.id;
                                        
                                        // Заголовок сцены
                                        AddTextToCSV(csv, sceneNode.title, $"Scene: {sceneSource}, Node: {nodeId}, Type: Title");
                                        
                                        // Описание сцены
                                        AddTextToCSV(csv, sceneNode.description, $"Scene: {sceneSource}, Node: {nodeId}, Type: Description");
                                        
                                        // Варианты ответов
                                        if (sceneNode?.list != null)
                                        {
                                            for (int i = 0; i < sceneNode.list.Count; i++)
                                            {
                                                var exit = sceneNode.list[i];
                                                AddTextToCSV(csv, exit.option, $"Scene: {sceneSource}, Node: {nodeId}, Type: Option {i + 1}, Target: {exit.target ?? ""}");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            csv.AppendLine(CreateCSVLine($"Error extracting scenes: {ex.Message}", "Error"));
        }

        File.WriteAllText(Path.Combine(directory, "scenes_dialogues.csv"), csv.ToString(), Encoding.UTF8);
        Debug.Log("Scenes and dialogues extracted to CSV");
    }

    /// <summary>
    /// Извлекает строковые списки
    /// </summary>
    private static void ExtractStringListsToFile(string directory)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Original;Translate;Comment");

        try
        {
            var projectStack = GetProjectStack();
            foreach (var project in projectStack)
            {
                if (project?.data?.stringListData?.list != null)
                {
                    foreach (var stringList in project.data.stringListData.list)
                    {
                        if (stringList?.list != null)
                        {
                            for (int i = 0; i < stringList.list.Count; i++)
                            {
                                var stringData = stringList.list[i];
                                AddTextToCSV(csv, stringData.description, $"StringList: {stringList.id}, Item: {i + 1}, ID: {stringData.id}");
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            csv.AppendLine(CreateCSVLine($"Error extracting string lists: {ex.Message}", "Error"));
        }

        File.WriteAllText(Path.Combine(directory, "string_lists.csv"), csv.ToString(), Encoding.UTF8);
        Debug.Log("String lists extracted to CSV");
    }

    /// <summary>
    /// Извлекает все предметы
    /// </summary>
    private static void ExtractItemsToFile(string directory)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Original;Translate;Comment");

        try
        {
            var projectStack = GetProjectStack();
            foreach (var project in projectStack)
            {
                var itemContainer = project.itemContainer;
                
                // Все категории предметов
                ExtractItemCategory(csv, "MELEE WEAPONS", itemContainer?.meleeWeapons?.list);
                ExtractItemCategory(csv, "RANGED WEAPONS", itemContainer?.rangedWeapons?.list);
                ExtractItemCategory(csv, "ARMOR", itemContainer?.armor?.list);
                ExtractItemCategory(csv, "SHIELDS", itemContainer?.shields?.list);
                ExtractItemCategory(csv, "CLOTHING", itemContainer?.clothing?.list);
                ExtractItemCategory(csv, "ACCESSORIES", itemContainer?.accessories?.list);
                ExtractItemCategory(csv, "FOOD", itemContainer?.foods?.list);
                ExtractItemCategory(csv, "CONSUMABLES", itemContainer?.consumeables?.list);
                ExtractItemCategory(csv, "REAGENTS", itemContainer?.reagents?.list);
                ExtractItemCategory(csv, "GEMS", itemContainer?.gems?.list);
                ExtractItemCategory(csv, "JEWELRY", itemContainer?.jewelry?.list);
                ExtractItemCategory(csv, "TRINKETS", itemContainer?.trinkets?.list);
                ExtractItemCategory(csv, "ADVENTURING ITEMS", itemContainer?.adventuringItems?.list);
                ExtractItemCategory(csv, "KEYS", itemContainer?.keys?.list);
                ExtractItemCategory(csv, "MISCELLANEOUS", itemContainer?.miscItems?.list);
            }
        }
        catch (Exception ex)
        {
            csv.AppendLine(CreateCSVLine($"Error extracting items: {ex.Message}", "Error"));
        }

        File.WriteAllText(Path.Combine(directory, "items.csv"), csv.ToString(), Encoding.UTF8);
        Debug.Log("Items extracted to CSV");
    }

    /// <summary>
    /// Извлекает категорию предметов
    /// </summary>
    private static void ExtractItemCategory<T>(StringBuilder csv, string categoryName, List<T> items) 
        where T : SKALDProjectData.ItemDataContainers.ItemData
    {
        if (items == null || items.Count == 0) return;
        
        try
        {
            foreach (var item in items)
            {
                AddTextToCSV(csv, item.title, $"Item: {item.id}, Category: {categoryName}, Field: Title");
                AddTextToCSV(csv, item.description, $"Item: {item.id}, Category: {categoryName}, Field: Description");
            }
        }
        catch (Exception ex)
        {
            csv.AppendLine(CreateCSVLine($"Error extracting {categoryName}: {ex.Message}", "Error"));
        }
    }

    /// <summary>
    /// Извлекает книги отдельно (с полным содержимым)
    /// </summary>
    private static void ExtractBooksToFile(string directory)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Original;Translate;Comment");

        try
        {
            var projectStack = GetProjectStack();
            foreach (var project in projectStack)
            {
                if (project?.itemContainer?.books?.list != null)
                {
                    foreach (var book in project.itemContainer.books.list)
                    {
                        AddTextToCSV(csv, book.title, $"Book: {book.id}, Field: Title");
                        AddTextToCSV(csv, book.description, $"Book: {book.id}, Field: Description");
                        
                        // Содержимое книги разбиваем на части
                        if (!string.IsNullOrEmpty(book.content))
                        {
                            var sentences = GameTextParser.Parse(book.content);
                            for (int i = 0; i < sentences.Count; i++)
                            {
                                csv.AppendLine(CreateCSVLine(sentences[i], $"Book: {book.id}, Field: Content, Part: {i + 1}"));
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            csv.AppendLine(CreateCSVLine($"Error extracting books: {ex.Message}", "Error"));
        }

        File.WriteAllText(Path.Combine(directory, "books.csv"), csv.ToString(), Encoding.UTF8);
        Debug.Log("Books extracted to CSV");
    }

    /// <summary>
    /// Извлекает персонажей
    /// </summary>
    private static void ExtractCharactersToFile(string directory)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Original;Translate;Comment");

        try
        {
            var projectStack = GetProjectStack();
            foreach (var project in projectStack)
            {
                var characterContainer = project.characterContainer;
                
                // Извлекаем всех персонажей из всех контейнеров
                ExtractCharacterContainer(csv, "UNIQUE HUMANOIDS", characterContainer?.uniqueHumanoids?.list);
                ExtractCharacterContainer(csv, "COMMON HUMANOIDS", characterContainer?.commonHumanoids?.list);
                ExtractCharacterContainer(csv, "ANIMALS", characterContainer?.animals?.list);
                ExtractCharacterContainer(csv, "MONSTERS", characterContainer?.monsters?.list);
            }
        }
        catch (Exception ex)
        {
            csv.AppendLine(CreateCSVLine($"Error extracting characters: {ex.Message}", "Error"));
        }

        File.WriteAllText(Path.Combine(directory, "characters.csv"), csv.ToString(), Encoding.UTF8);
        Debug.Log("Characters extracted to CSV");
    }

    private static void ExtractCharacterContainer<T>(StringBuilder csv, string containerName, List<T> characters)
        where T : SKALDProjectData.CharacterContainers.Character
    {
        if (characters == null || characters.Count == 0) return;
        
        try
        {
            foreach (var character in characters)
            {
                AddTextToCSV(csv, character.title, $"Character: {character.id}, Type: {containerName}, Field: Title");
                AddTextToCSV(csv, character.description, $"Character: {character.id}, Type: {containerName}, Field: Description");
            }
        }
        catch (Exception ex)
        {
            csv.AppendLine(CreateCSVLine($"Error extracting {containerName}: {ex.Message}", "Error"));
        }
    }

    /// <summary>
    /// Извлекает квесты
    /// </summary>
    private static void ExtractQuestsToFile(string directory)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Original;Translate;Comment");

        try
        {
            var projectStack = GetProjectStack();
            foreach (var project in projectStack)
            {
                var questContainers = project.questContainers;
                
                if (questContainers?.mainQuests?.list != null)
                {
                    foreach (var quest in questContainers.mainQuests.list)
                    {
                        ExtractQuest(csv, quest, "MAIN QUEST");
                    }
                }
                
                if (questContainers?.sideQuests?.list != null)
                {
                    foreach (var quest in questContainers.sideQuests.list)
                    {
                        ExtractQuest(csv, quest, "SIDE QUEST");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            csv.AppendLine(CreateCSVLine($"Error extracting quests: {ex.Message}", "Error"));
        }

        File.WriteAllText(Path.Combine(directory, "quests.csv"), csv.ToString(), Encoding.UTF8);
        Debug.Log("Quests extracted to CSV");
    }

    /// <summary>
    /// Извлекает отдельный квест
    /// </summary>
    private static void ExtractQuest(StringBuilder csv, SKALDProjectData.QuestContainers.QuestData quest, string questType)
    {
        AddTextToCSV(csv, quest.title, $"Quest: {quest.id}, Type: {questType}, Field: Title");
        AddTextToCSV(csv, quest.begunDescription, $"Quest: {quest.id}, Type: {questType}, Field: Begun");
        AddTextToCSV(csv, quest.completedDescription, $"Quest: {quest.id}, Type: {questType}, Field: Completed");
        AddTextToCSV(csv, quest.failedDescription, $"Quest: {quest.id}, Type: {questType}, Field: Failed");
        AddTextToCSV(csv, quest.aboutDescription, $"Quest: {quest.id}, Type: {questType}, Field: About");
        AddTextToCSV(csv, quest.rewardDescription, $"Quest: {quest.id}, Type: {questType}, Field: Reward");
    }

    /// <summary>
    /// Извлекает журнал
    /// </summary>
    private static void ExtractJournalToFile(string directory)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Original;Translate;Comment");

        try
        {
            var projectStack = GetProjectStack();
            foreach (var project in projectStack)
            {
                var journalContainers = project.journalContainers;
                
                ExtractJournalContainer(csv, "Chapter 0", journalContainers?.chapter0Container?.list);
                ExtractJournalContainer(csv, "Chapter 1", journalContainers?.chapter1Container?.list);
                ExtractJournalContainer(csv, "Chapter 2", journalContainers?.chapter2Container?.list);
                ExtractJournalContainer(csv, "Characters", journalContainers?.charactersContainer?.list);
                ExtractJournalContainer(csv, "Miscellaneous", journalContainers?.miscContainer?.list);
            }
        }
        catch (Exception ex)
        {
            csv.AppendLine(CreateCSVLine($"Error extracting journal: {ex.Message}", "Error"));
        }

        File.WriteAllText(Path.Combine(directory, "journal.csv"), csv.ToString(), Encoding.UTF8);
        Debug.Log("Journal extracted to CSV");
    }

    private static void ExtractJournalContainer(StringBuilder csv, string chapterName, List<SKALDProjectData.JournalContainers.JournalEntry> entries)
    {
        if (entries == null || entries.Count == 0) return;
        
        try
        {
            foreach (var entry in entries)
            {
                AddTextToCSV(csv, entry.title, $"Journal: {chapterName}, Entry: {entry.id}, Field: Title");
                AddTextToCSV(csv, entry.description, $"Journal: {chapterName}, Entry: {entry.id}, Field: Description");
            }
        }
        catch (Exception ex)
        {
            csv.AppendLine(CreateCSVLine($"Error extracting {chapterName}: {ex.Message}", "Error"));
        }
    }

    /// <summary>
    /// Извлекает способности и заклинания
    /// </summary>
    private static void ExtractAbilitiesAndSpellsToFile(string directory)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Original;Translate;Comment");

        try
        {
            var projectStack = GetProjectStack();
            foreach (var project in projectStack)
            {
                var abilities = project.abilityContainers;
                
                if (abilities?.spellContainer?.list != null)
                {
                    foreach (var spell in abilities.spellContainer.list)
                    {
                        AddTextToCSV(csv, spell.title, $"Ability: {spell.id}, Type: Spell, Field: Title");
                        AddTextToCSV(csv, spell.description, $"Ability: {spell.id}, Type: Spell, Field: Description");
                    }
                }
                
                if (abilities?.combatManeuverContainer?.list != null)
                {
                    foreach (var maneuver in abilities.combatManeuverContainer.list)
                    {
                        AddTextToCSV(csv, maneuver.title, $"Ability: {maneuver.id}, Type: Combat Maneuver, Field: Title");
                        AddTextToCSV(csv, maneuver.description, $"Ability: {maneuver.id}, Type: Combat Maneuver, Field: Description");
                    }
                }
                
                if (abilities?.additionAbilityContainer?.list != null)
                {
                    foreach (var ability in abilities.additionAbilityContainer.list)
                    {
                        AddTextToCSV(csv, ability.title, $"Ability: {ability.id}, Type: Passive Ability, Field: Title");
                        AddTextToCSV(csv, ability.description, $"Ability: {ability.id}, Type: Passive Ability, Field: Description");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            csv.AppendLine(CreateCSVLine($"Error extracting abilities: {ex.Message}", "Error"));
        }

        File.WriteAllText(Path.Combine(directory, "abilities_spells.csv"), csv.ToString(), Encoding.UTF8);
        Debug.Log("Abilities and spells extracted to CSV");
    }

    /// <summary>
    /// Получает путь к директории плагина
    /// </summary>
    private static string GetPluginDirectory()
    {
        try
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            return Path.GetDirectoryName(assembly.Location);
        }
        catch
        {
            return Application.dataPath;
        }
    }

    /// <summary>
    /// Получает стек проектов из GameData
    /// </summary>
    private static List<SKALDProjectData> GetProjectStack()
    {
        try
        {
            // Используем рефлексию для доступа к приватному полю projectStack
            var gameDataType = typeof(GameData);
            var projectStackField = gameDataType.GetField("projectStack", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            return (List<SKALDProjectData>)projectStackField?.GetValue(null) ?? new List<SKALDProjectData>();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to get project stack: {ex.Message}");
            return new List<SKALDProjectData>();
        }
    }
}

public static class GameTextParser
{
    /* ─────────────────── ПУБЛИЧНЫЙ API ─────────────────── */

    public static List<string> Parse(string raw)
    {
        if (raw == null) throw new ArgumentNullException(nameof(raw));

        // 0) убрать тех-хвосты ";;Scene …"
        raw = PreNormalize(raw);
        raw = Regex.Replace(raw, @";;.*?$", "", RegexOptions.Multiline);

        // 1) раскрыть #IF/#THEN/#ELSE/#END
        var variants = ExpandFirstIf(raw);

        // 2) каждую ветвь → предложения
        var outList = new List<string>();
        foreach (var v in variants)
            outList.AddRange(SplitIntoSentences(v));

        return outList;
    }

    /* ──────────────── #IF / #THEN / #ELSE ──────────────── */

    private static List<string> ExpandFirstIf(string src)
    {
        // скобочная форма
        const string PAT_PAREN =
            @"#IF\s*\([^\)]*\)\s*#THEN\s*\((.*?)\)(?:\s*#ELSE\s*\((.*?)\))?\s*#END";
        // «сырая» форма
        const string PAT_RAW =
            @"#IF\s*\([^\)]*\)\s*#THEN\s*(.+?)(?:#ELSE\s*(.+?))?\s*#END";

        var m = Regex.Match(src, PAT_PAREN,
                            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!m.Success)
            m = Regex.Match(src, PAT_RAW,
                            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!m.Success)
            return new List<string> { src.Trim() };

        string full     = m.Value;
        string thenPart = StripOuterQuotes(m.Groups[1].Value.Trim());
        string elsePart = m.Groups.Count > 2 && m.Groups[2].Success
                        ? StripOuterQuotes(m.Groups[2].Value.Trim())
                        : "";

        var list = new List<string>();
        string prefix = src.Substring(0, m.Index);
        string suffix = src.Substring(m.Index + full.Length);

        foreach (var v in ExpandFirstIf(prefix + thenPart + suffix))
            list.Add(v);
        if (elsePart.Length > 0)
            foreach (var v in ExpandFirstIf(prefix + elsePart + suffix))
                list.Add(v);

        return list;
    }

    /* ─────────────── СЕГМЕНТАЦИЯ ПРЕДЛОЖЕНИЙ ───────────── */

    private static IEnumerable<string> SplitIntoSentences(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;

        string flat = Regex.Replace(text, @"\r?\n+", " ").Trim();
        flat = PreMaskAbbr(flat);

        string[] parts = Regex.Split(
            flat,
            @"(?<=[\.!?…]['""”)]?)\s+(?=[“""']?[A-Z])" + // 1. обычный финал
            @"|(?<=[\.!?…]['""”])\s+(?=[a-z])" +                // 2. кавычка + маленькая
            @"|(?<=[""”])\s+(?=[A-Z])" +                     // 3. кавычка + Заглавная
            @"|(?<=:)\s+(?=[“""']?[A-Z])" +                  // 4. заголовок:
            @"|(?<=,\s*[""“])\s*(?=[A-Z])" +                 // 5. ЗАПЯТАЯ + кавычка
            @"|(?<=[\.!?…]['""”]),\s+(?=[A-Z])" +            // 6.  !" ,  после закрыв. кавычки
            @"|(?<=,['""“”])\s+(?=[A-Za-z])" +                  // 7.  ,"  — запятая внутри цитаты
            @"|(?<=[\.!?…]['""”)]?)\s+[“""']?\.\.\.\s*(?=[A-Z])" +  // 8bis ← НОВОЕ
            @"|(?<=[\.!?…])\s+[""“”]\s*-\s+(?=[A-Z])" +
            @"|(?<=\|PAR\|)" + 
            @"|(?<=:)\s*(?=\|PAR\|)" +
            @"|(?<=\S)\s*~\s*(?=\S)" +
            @"|(?<=:)\s+(?=[“""']?\{)" +
            @"|(?<=[\.!?…])\s+(?=\(\s*[“""']?[A-Z])" +
            @"|(?<=\)[”""']?)\s+(?=[A-Z])" +
            @"|(?<=\)[”""']?)\s+\(\s*(?=[A-Z])" +
            @"|(?<=[”""'])\)\s+\(\s*(?=[A-Z])" +
            @"|(?<=\.)\s+(?=[\+\-]\d)"
        );

        foreach (string raw in parts)
        {
            string s = raw.Trim();
            if (s.Length == 0) continue;

            s = CleanPart(s);

            var htmlParts = SplitHtmlParts(s);
            foreach (var html in htmlParts)
            {
                var curlyParts = SplitCurlyParts(html);
                foreach (var cur in curlyParts)
                {
                    var squareParts = SplitSquareBracketParts(cur);
                    foreach (var part in squareParts)
                    {
                        string clean = CleanPart(part);
                        foreach (var sub in PostSplitCommaCaps(clean))
                        {
                            string final = CleanPart(sub);
                            if (final.Length > 0)
                                yield return final;
                        }
                    }
                }
            }
        }
    }

    /* ─────────────── ВСПОМОГАТЕЛЬНЫЕ ─────────────── */

    private static readonly char[] QuoteChars = { '"', '“', '”', '«', '»' };
    private static bool IsQuote(char c) => Array.IndexOf(QuoteChars, c) >= 0;

    private static string StripOuterQuotes(string s)
    {
        s = s.Trim();
        if (s.Length < 2) return s;

        char first = s[0], last = s[s.Length - 1];
        bool pair = IsQuote(first) && IsQuote(last);

        if (pair &&
            s.IndexOf(first, 1) == -1 &&
            s.LastIndexOf(last, s.Length - 2) == -1)
            return s.Substring(1, s.Length - 2).Trim();

        return s;
    }

    private static string TrimEdges(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;

        // ---- удалить лидирующие маркёры и символы ----
        while (true)
        {
            if (s.StartsWith("|PAR|", StringComparison.Ordinal))
                s = s.Substring(5);
            else if (s.Length > 0 &&
                    (s[0] == Mask || char.IsWhiteSpace(s[0]) || s[0] == '*' ||
                    s[0] == '«' || s[0] == '»' || s[0] == '“' || s[0] == '”' ||
                    s[0] == '-' || s[0] == '.' || s[0] == '…' ||
                    s[0] == '('))
                s = s.Substring(1);
            else break;
        }

        // ---- удалить замыкающие маркёры и символы ----
        while (true)
        {
            if (s.EndsWith("|PAR|", StringComparison.Ordinal))
                s = s.Substring(0, s.Length - 5);
            else if (s.Length > 0 &&
                    (s[^1] == Mask || char.IsWhiteSpace(s[^1]) || s[^1] == '*' ||
                    s[^1] == '«' || s[^1] == '»' || s[^1] == '“' || s[^1] == '”' ||
                    s[^1] == '.' || s[^1] == '…' || s[^1] == ':' || 
                    s[^1] == '-' || s[^1] == ')')) 
                s = s.Substring(0, s.Length - 1);
            else break;
        }

        return s.Trim();
    }

    /// <summary>Убирает все подряд кавычки по краям.</summary>
    private static string StripOuterSentenceQuotes(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;

        /* удалить лидирующие кавычки */
        int start = 0;
        while (start < s.Length && IsQuote(s[start]))
            start++;
        s = s.Substring(start);

        if (s.Length == 0) return s;

        /* удалить замыкающие кавычки (учитываем вариант ".) */
        int end = s.Length - 1;
        while (end >= 0 &&
            (IsQuote(s[end]) ||
            (end > 0 && IsQuote(s[end - 1]) && ".!?".IndexOf(s[end]) >= 0)))
            end--;
        s = s.Substring(0, end + 1);

        return s.Trim();
    }

    /// <summary>
    /// Извлекает действия вида [Something] и одновременно удаляет их из строки.
    /// Возвращает список: [0] – строка без скобок (может быть пустой),
    /// далее – каждое действие без скобок.
    /// </summary>
    private static List<string> SplitSquareBracketParts(string src)
    {
        var list = new List<string>();
        var actions = Regex.Matches(src, @"\[[^\]]+\]");
        string without = Regex.Replace(src, @"\[[^\]]+\]", "").Trim();
        if (without.Length > 0) list.Add(without);

        foreach (Match m in actions)
        {
            string act = m.Value.Substring(1, m.Value.Length - 2).Trim();
            if (act.Length > 0) list.Add(act);
        }
        return list;
    }

    /// <summary>
    /// Извлекает содержимое HTML-тегов <tag>…</tag>, возвращая:
    /// [0] – строка без тегов (может быть пустой),
    /// далее – каждый внутренний текст тега (уже без тегов).
    /// </summary>
    private static List<string> SplitHtmlParts(string src)
    {
        // 1) разбить по любому тегу  < ... >
        var tokens = Regex.Split(src, @"<[^>]+>");
        var list = new List<string>();

        foreach (var t in tokens)
        {
            string txt = t.Trim();
            if (txt.Length > 0)
                list.Add(txt);       // добавляем только непустые фрагменты
        }
        return list;
    }

    /// <summary>
    /// Обрабатывает конструкции {...}.
    /// • {getName}     → подставляем {PLAYER}
    /// • {getMoney}    → подставляем {MONEY}
    /// • {addXp|300}   → подставляем 300
    /// • {lordLady}    → создаём две строки: lord / lady
    /// Остальные {fooBar} удаляются.
    /// </summary>
    private static List<string> SplitCurlyParts(string src)
    {
        // начнём с одной версии – исходная строка
        var results = new List<string> { src };

        // 1. сначала обрабатываем ветвление lordLady
        for (int idx = 0; idx < results.Count; idx++)
        {
            string cur = results[idx];
            var m = Regex.Match(cur, @"\{lordLady\}", RegexOptions.IgnoreCase);
            if (!m.Success) continue;

            // создаём две версии строки
            string lord = cur.Replace(m.Value, "lord");
            string lady = cur.Replace(m.Value, "lady");

            // заменяем текущий, добавляем второй
            results[idx] = lord;
            results.Insert(idx + 1, lady);
        }

        // 2. для каждой строки делаем точечные подстановки / удаления
        for (int i = 0; i < results.Count; i++)
        {
            string line = results[i];

            // единый проход: подменяем или убираем каждую {…}-команду
            line = Regex.Replace(
                line,
                @"\{([^{}]+)\}",                       // захватываем внутренний токен
                match =>
                {
                    string token = match.Groups[1].Value;

                    // ---- 1. getName → {PLAYER}
                    if (token.Equals("getName", StringComparison.OrdinalIgnoreCase))
                        return "{PLAYER}";

                    // ---- 2. addXp|NNN → NNN
                    if (token.StartsWith("addXp", StringComparison.OrdinalIgnoreCase))
                    {
                        int sep = token.IndexOf('|');
                        if (sep > 0 && int.TryParse(token.Substring(sep + 1), out int xp))
                            return xp.ToString();
                        return "0";
                    }

                    // ---- 3. getMoney / getGold → {MONEY}
                    if (token.StartsWith("getMoney", StringComparison.OrdinalIgnoreCase) ||
                        token.StartsWith("getGold",  StringComparison.OrdinalIgnoreCase))
                        return "{MONEY}";

                    // ---- 4. остальные {fooBar} → удалить
                    return string.Empty;
                },
                RegexOptions.IgnoreCase);

            // финальная очистка
            line = line.Trim();
            results[i] = line;
        }

        // удалить пустые после очистки
        results.RemoveAll(string.IsNullOrEmpty);
        return results;
    }

    /// <summary>Подготавливает текст:
    ///  – вырезает строки, содержащие только *;
    ///  – заменяет двойные \n\n на метку |PAR|, чтобы сохранить пустые абзацы.</summary>
    private static string PreNormalize(string src)
    {
        // 0-a) "# -IF"  →  "#IF"
        src = Regex.Replace(src, @"#\s*-\s*IF", "#IF",
                            RegexOptions.IgnoreCase);

        // 0-b) "-)#ELSE" или "-)#END"  →  "#ELSE/#END"
        src = Regex.Replace(src, @"-\s*\)\s*#(ELSE|END)", ")#$1", RegexOptions.IgnoreCase);

        // 0-c) ")#ELSE" или ")#END"  →  "#ELSE/#END"
        src = Regex.Replace(src, @"\)\s*#(ELSE|END)", ")#$1", RegexOptions.IgnoreCase);

        // 1) удалить строки, содержащие только "*"
        src = Regex.Replace(src, @"^\s*\*\s*$", "", RegexOptions.Multiline);

        // 2) если строка начинается с ENTRY N —-→ вставить |PAR| после неё
        src = Regex.Replace(src,
            @"^(ENTRY\s+\d+.*)$",
            "$1|PAR|",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        // 3) двойной перевод строки → |PAR|
        src = Regex.Replace(src, @"\r?\n\s*\r?\n", "|PAR|");

        return src;
    }

    private const char Mask = '\uE000';   // маркёр-обёртка

    // A-Z или a-z (1–4 символа) + точка, после которой пробел / конец / знаки ,;:!? – маскируем
    private static string PreMaskAbbr(string s)
    {
        foreach (var abbr in KnownAbbr)
        {
            // \b — граница слова, чтобы не ловить 'NPD.MG.' и т.п.
            string pattern = $@"\b{Regex.Escape(abbr)}";
            // U+E000 оборачивает ВЕСЬ аббр-токен
            s = Regex.Replace(
                    s, pattern,
                    m => $"{Mask}{m.Value}{Mask}",          // сохраняем оригинальный регистр
                    RegexOptions.IgnoreCase);
        }
        return s;
    }

    private static string PostUnmaskAbbr(string s)
    {
        return s.Replace(Mask.ToString(), "");
    }

    private static IEnumerable<string> PostSplitCommaCaps(string line)
    {
        // проверяем: все «словные» токены начинаются с заглавной
        // (разрешаем дефис - для "Light Club")
        var words = Regex.Matches(line, @"\b[^\W\d_]+\b");
        if (words.Count == 0) { yield return line; yield break; }

        bool allCapsStart = words.Cast<Match>()
                                .All(m => char.IsUpper(m.Value[0]));
        if (!allCapsStart) { yield return line; yield break; }

        // делим по запятой, числу или двойном пробеле
        foreach (var part in Regex.Split(line, @"\s*,\s*|\s+\d+\s+"))
        {
            string p = part.Trim();
            if (p.Length > 0) yield return p;
        }
    }

    /// Полная финальная очистка одного текстового фрагмента.
    private static string CleanPart(string txt)
    {
        txt = StripOuterSentenceQuotes(txt);

        /* лидирующий +1 / 1) / 1. */
        txt = Regex.Replace(txt,
                    @"^[\+\-]?\d+(?:\.\d+)?%?\s*(?:[)\.]\s*|\s+)",
                    "");

        txt = PostUnmaskAbbr(txt);                  // вернуть точки аббревиатур
        
        if (Regex.IsMatch(txt.Trim(), @"^\{(PLAYER|MONEY)\}$", RegexOptions.IgnoreCase))
            return "";

        /* множитель  x1  x1.5  :x2 */
        txt = Regex.Replace(txt, @"\s*[:]?x\d+(\.\d+)?\b",
                            "", RegexOptions.IgnoreCase);

        /*  скобки без букв:  (10)  (02:00)  (5  */
        txt = Regex.Replace(txt,
                @"\s*\([^A-Za-z)]*\)\s*$", "");
        
        if (Regex.IsMatch(txt, @"^\s*\([^A-Za-z]*$"))
            return "";

        /*  конечный числовой диапазон / дробь:  1-3  3/4  15 */
        txt = Regex.Replace(txt,
                @"\s+\d+(?:[-/]\d+)*\s*$", "");

        /* если остался «голый» числовой токен — не выводим строку */
        if (Regex.IsMatch(txt, @"^\d+([./]\d+)*(\s*[A-Za-z]+)?$",
                        RegexOptions.IgnoreCase))
            return "";

        /* схлопнуть повторные пробелы */
        txt = Regex.Replace(txt, @"\s{2,}", " ").Trim();

        /* если в строке нет букв — пропустим её */
        if (!Regex.IsMatch(txt, @"[A-Za-z]"))
            return "";

        txt = TrimEdges(txt);
        return txt;
    }

    private static readonly string[] KnownAbbr =
    {
        "P.", "DMG.", "STR.", "DEX.", "INT.", "CHA.", "CON.",
        "HP.", "AC.", "DC.", "SPD.", "PER.", "WIS.", "AGI.",
        "LVL."
    };
}