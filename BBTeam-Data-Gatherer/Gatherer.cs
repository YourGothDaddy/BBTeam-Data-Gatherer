namespace BBTeam_Data_Gatherer
{
    using AngleSharp.Html.Dom;
    using AngleSharp.Html.Parser;
    using BBTeam_Data_Gatherer.Models;
    using System;
    using System.Globalization;
    using System.Text.Encodings.Web;
    using System.Text.Json;
    using System.Text.RegularExpressions;

    public class Gatherer
    {
        private HtmlParser _parser;
        private HttpClient _httpClient;

        public Gatherer()
        {
            this._parser = new HtmlParser();
            this._httpClient = new HttpClient();
        }

        public async Task Gather()
        {
            var categoryUrlsAndTitle = await GetAllCategoryUrlsAsync();
            var consumableItemUrls = await GetAllConsumableItemUrlsAsync(categoryUrlsAndTitle);
            var consumableItems = await CreateConsumableItems(consumableItemUrls.Keys.ToList(), categoryUrlsAndTitle, consumableItemUrls);
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,  // Optional: makes the JSON human-readable
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // Allows Cyrillic characters
            };
            string jsonString = JsonSerializer.Serialize(consumableItems, options);
            Console.WriteLine(jsonString);
            File.WriteAllText("consumableItem.json", jsonString);
            Console.WriteLine("JSON successfully saved to consumableItem.json");
        }

        public async Task<Dictionary<string, string>> GetAllCategoryUrlsAsync()
        {
            var url = "https://www.bb-team.org/hrani";
            string htmlContent = null;

            var categoriesResult = new Dictionary<string, string>();
            try
            {
                var response = await _httpClient.GetAsync(url);
                htmlContent = await response.Content.ReadAsStringAsync();

                if (!string.IsNullOrEmpty(htmlContent))
                {
                    var document = await _parser.ParseDocumentAsync(htmlContent);

                    var categories = document.GetElementsByClassName("relative flex flex-col items-center p-4 mb-4 bg-white rounded-lg shadow");

                    if (categories != null)
                    {
                        foreach (var category in categories)
                        {
                            var categoryLinkNode = category.GetElementsByTagName("a");
                            var categoryLink = categoryLinkNode[0].GetAttribute("href");
                            var categoryTitle = category.QuerySelector("h2[class='text-center']")?.TextContent.Trim();

                            categoriesResult[categoryLink] = categoryTitle;
                        }

                        return categoriesResult;
                    }
                }
            }
            catch
            {
                Console.WriteLine("Could not get all categories!");
            }

            return null;
        }

        public async Task<Dictionary<string, string>> GetAllConsumableItemUrlsAsync(Dictionary<string, string> categoryUrlsAndTitle)
        {
            var categoryUrls = categoryUrlsAndTitle.Keys.ToList();
            string htmlContent = null;

            var consumableItemUrls = new Dictionary<string, string>();

            foreach (var categoryUrl in categoryUrls)
            {
                try
                {
                    var response = await _httpClient.GetAsync(categoryUrl);
                    htmlContent = await response.Content.ReadAsStringAsync();

                    if (!string.IsNullOrEmpty(htmlContent))
                    {
                        var document = await _parser.ParseDocumentAsync(htmlContent);

                        var consumableItems = document.GetElementsByClassName("relative flex flex-col items-center p-4 mb-4 bg-white rounded-lg shadow");
                            
                        if (consumableItems != null)
                        {
                            foreach (var consumableItem in consumableItems)
                            {
                                var consumableItemLinkNode = consumableItem.GetElementsByTagName("a");
                                var consumableItemLink = consumableItemLinkNode[0].GetAttribute("href");

                                consumableItemUrls[consumableItemLink] = categoryUrlsAndTitle[categoryUrl];
                            }
                        }
                    }
                }
                catch
                {
                    Console.WriteLine("Could not get a consumableItem");
                }
            }

            return consumableItemUrls;
        }

        public async Task<List<ConsumableItem>> CreateConsumableItems(List<string> consumableItemUrls1, Dictionary<string, string> categoryUrlsAndTitle, Dictionary<string, string> consumableItemUrls)
        {
            string htmlContent = null;

            var consumableItems = new List<ConsumableItem>();
            int consumableItemCount = 0;
            await Console.Out.WriteLineAsync($"Consumable items: {consumableItemUrls1.Count}");
            foreach (var consumableItemUrl in consumableItemUrls1)
            {
                try
                {
                    var response = await _httpClient.GetAsync(consumableItemUrl);
                    htmlContent = await response.Content.ReadAsStringAsync();

                    if (!string.IsNullOrEmpty(htmlContent))
                    {
                        var document = await _parser.ParseDocumentAsync(htmlContent);

                        var categoryTitle = consumableItemUrls[consumableItemUrl];
                        var title = CleanText(document.QuerySelector("h1[itemprop='name']")?.TextContent);
                        var subtitle = CleanText(document.QuerySelector("p[itemprop='description']")?.TextContent);
                        float? caloriesPer100g = TryParseFloat(document.QuerySelector("span[itemprop='calories']")?.TextContent);
                        float? proteinPer100g = TryParseFloat(document.QuerySelector("span[itemprop='proteinContent']")?.TextContent);
                        float? carbohydratesPer100g = TryParseFloat(document.QuerySelector("span[itemprop='carbohydrateContent']")?.TextContent);
                        float? fatsPer100g = TryParseFloat(document.QuerySelector("span[itemprop='fatContent']")?.TextContent);
                        Carbohydrates carbohydrates = CreateCarbohydratesItem(document);
                        Vitamins vitamins = CreateVitaminsItem(document);
                        AminoAcids aminoAcids = CreateAminoAcidsItem(document);
                        Fats fats = CreateFatsItem(document);
                        Minerals minerals = CreateMineralsItem(document);
                        Sterols sterols = CreateSterolsItem(document);
                        Other other = CreateOtherItem(document);

                        var consumableItem = new ConsumableItem{
                            MainCategory = categoryTitle,
                            Title = title,
                            SubTitle = subtitle,
                            CaloriesPer100g = caloriesPer100g,
                            ProteinPer100g = proteinPer100g,
                            CarbohydratesPer100g = carbohydratesPer100g,
                            FatsPer100g = fatsPer100g,
                            Carbohydrates = carbohydrates,
                            Vitamins = vitamins,
                            AminoAcids = aminoAcids,
                            Fats = fats,
                            Minerals = minerals,
                            Sterols = sterols,
                            Other = other
                        };

                        consumableItems.Add(consumableItem);
                        consumableItemCount += 1;
                        await Console.Out.WriteLineAsync($"{consumableItemCount}");
                    }
                }
                catch
                {
                    Console.WriteLine("Could not create a consumable item!");
                    break;
                }
            }

            return consumableItems;
        }

        private Carbohydrates CreateCarbohydratesItem(IHtmlDocument document)
        {
            var listOfCarbohydrates = new List<string> { "fiber", "starch", "sugars", 
                "galactose", "glucose-dextrose", "sucrose", 
                "lactose", "maltose", "fructose" };

            var carbohydratesItem = new Carbohydrates();

            foreach (var carbohydrate in listOfCarbohydrates)
            {
                try
                {
                    var carbohydrateElement = document.QuerySelector($"td:has(a[href*='{carbohydrate}']) + .text-right");

                    if (carbohydrateElement != null)
                    {
                        var carbohydrateValue = carbohydrateElement.TextContent.Trim();

                        carbohydrateValue = carbohydrateValue.Split(' ')[0];
                        var parsedCarbohydrateValue = TryParseFloat(carbohydrateValue);

                        switch (carbohydrate)
                        {
                            case "fiber":
                                carbohydratesItem.Fiber = parsedCarbohydrateValue;
                                break;

                            case "starch":
                                carbohydratesItem.Starch = parsedCarbohydrateValue;
                                break;

                            case "sugars":
                                carbohydratesItem.Sugars = parsedCarbohydrateValue;
                                break;

                            case "galactose":
                                carbohydratesItem.Galactose = parsedCarbohydrateValue;
                                break;

                            case "glucose-dextrose":
                                carbohydratesItem.Glucose = parsedCarbohydrateValue;
                                break;

                            case "sucrose":
                                carbohydratesItem.Sucrose = parsedCarbohydrateValue;
                                break;

                            case "lactose":
                                carbohydratesItem.Lactose = parsedCarbohydrateValue;
                                break;

                            case "maltose":
                                carbohydratesItem.Maltose = parsedCarbohydrateValue;
                                break;

                            case "fructose":
                                carbohydratesItem.Fructose = parsedCarbohydrateValue;
                                break;
                        }
                    }
                }
                catch
                {
                    Console.WriteLine("Could not add the carbohydrate");
                }
            }

            return carbohydratesItem;
        }

        private Vitamins CreateVitaminsItem(IHtmlDocument document)
        {
            var listOfCarbohydrates = new List<string> { "betaine", "vitamin-b1", "vitamin-b2",
                "vitamin-b3", "vitamin-b4", "vitamin-b5",
                "vitamin-b6", "vitamin-b9", "vitamin-b12",
                "vitamin-c", "vitamin-d", "vitamin-e",
                "vitamin-k1", "vitamin-k2-mk4"};

            var vitaminsItem = new Vitamins();

            foreach (var vitamin in listOfCarbohydrates)
            {
                try
                {
                    var vitaminElement = document.QuerySelector($"td:has(a[href*='{vitamin}']) + .text-right");

                    if (vitaminElement != null)
                    {
                        var vitaminValue = vitaminElement.TextContent.Trim();

                        vitaminValue = vitaminValue.Split(' ')[0];
                        var parsedVitaminValue = TryParseFloat(vitaminValue);

                        switch (vitamin)
                        {
                            case "betaine":
                                vitaminsItem.Betaine = parsedVitaminValue;
                                break;

                            case "vitamin-b1":
                                vitaminsItem.VitaminB1 = parsedVitaminValue;
                                break;

                            case "vitamin-b2":
                                vitaminsItem.VitaminB2 = parsedVitaminValue;
                                break;

                            case "vitamin-b3":
                                vitaminsItem.VitaminB3 = parsedVitaminValue;
                                break;

                            case "vitamin-b4":
                                vitaminsItem.VitaminB4 = parsedVitaminValue;
                                break;

                            case "vitamin-b5":
                                vitaminsItem.VitaminB5 = parsedVitaminValue;
                                break;

                            case "vitamin-b6":
                                vitaminsItem.VitaminB6 = parsedVitaminValue;
                                break;

                            case "vitamin-b9":
                                vitaminsItem.VitaminB9 = parsedVitaminValue;
                                break;

                            case "vitamin-b12":
                                vitaminsItem.VitaminB12 = parsedVitaminValue;
                                break;

                            case "vitamin-c":
                                vitaminsItem.VitaminC = parsedVitaminValue;
                                break;

                            case "vitamin-d":
                                vitaminsItem.VitaminD = parsedVitaminValue;
                                break;

                            case "vitamin-e":
                                vitaminsItem.VitaminE = parsedVitaminValue;
                                break;

                            case "vitamin-k1":
                                vitaminsItem.VitaminK1 = parsedVitaminValue;
                                break;

                            case "vitamin-k2-mk4":
                                vitaminsItem.VitaminK2 = parsedVitaminValue;
                                break;
                        }
                    }
                }
                catch
                {
                    Console.WriteLine("Could not add the vitamins!");
                }
            }

            return vitaminsItem;
        }

        private AminoAcids CreateAminoAcidsItem(IHtmlDocument document)
        {
            var listOfAminoAcids = new List<string> { "alanine", "arginine", "aspartic-acid",
        "valine", "glycine", "glutamic-acid", "isoleucine",
        "leucine", "lysine", "methionine", "proline",
        "serine", "tyrosine", "threonine", "tryptophan",
        "phenylalanine", "hydroxyproline", "histidine", "cystine" };

            var aminoAcidsItem = new AminoAcids();

            foreach (var aminoAcid in listOfAminoAcids)
            {
                try
                {
                    var aminoAcidElement = document.QuerySelector($"td:has(a[href*='{aminoAcid}']) + .text-right");

                    if (aminoAcidElement != null)
                    {
                        var aminoAcidValue = aminoAcidElement.TextContent.Trim();

                        aminoAcidValue = aminoAcidValue.Split(' ')[0];
                        var parsedAminoAcidValue = TryParseFloat(aminoAcidValue);

                        switch (aminoAcid)
                        {
                            case "alanine":
                                aminoAcidsItem.Alanine = parsedAminoAcidValue;
                                break;

                            case "arginine":
                                aminoAcidsItem.Arginine = parsedAminoAcidValue;
                                break;

                            case "aspartic-acid":
                                aminoAcidsItem.AsparticAcid = parsedAminoAcidValue;
                                break;

                            case "valine":
                                aminoAcidsItem.Valine = parsedAminoAcidValue;
                                break;

                            case "glycine":
                                aminoAcidsItem.Glycine = parsedAminoAcidValue;
                                break;

                            case "glutamic-acid":
                                aminoAcidsItem.Glutamine = parsedAminoAcidValue;
                                break;

                            case "isoleucine":
                                aminoAcidsItem.Isoleucine = parsedAminoAcidValue;
                                break;

                            case "leucine":
                                aminoAcidsItem.Leucine = parsedAminoAcidValue;
                                break;

                            case "lysine":
                                aminoAcidsItem.Lysine = parsedAminoAcidValue;
                                break;

                            case "methionine":
                                aminoAcidsItem.Methionine = parsedAminoAcidValue;
                                break;

                            case "proline":
                                aminoAcidsItem.Proline = parsedAminoAcidValue;
                                break;

                            case "serine":
                                aminoAcidsItem.Serine = parsedAminoAcidValue;
                                break;

                            case "tyrosine":
                                aminoAcidsItem.Tyrosine = parsedAminoAcidValue;
                                break;

                            case "threonine":
                                aminoAcidsItem.Threonine = parsedAminoAcidValue;
                                break;

                            case "tryptophan":
                                aminoAcidsItem.Tryptophan = parsedAminoAcidValue;
                                break;

                            case "phenylalanine":
                                aminoAcidsItem.Phenylalanine = parsedAminoAcidValue;
                                break;

                            case "hydroxyproline":
                                aminoAcidsItem.Hydroxyproline = parsedAminoAcidValue;
                                break;

                            case "histidine":
                                aminoAcidsItem.Histidine = parsedAminoAcidValue;
                                break;

                            case "cystine":
                                aminoAcidsItem.Cystine = parsedAminoAcidValue;
                                break;
                        }
                    }
                }
                catch
                {
                    Console.WriteLine("Could not add the amino acid!");
                }
            }

            return aminoAcidsItem;
        }

        private Fats CreateFatsItem(IHtmlDocument document)
        {
            var listOfFats = new List<string> { "lipids", "monounsaturated-acids", "polyunsaturated-acids",
        "saturated-acids", "trans-acids" };

            var fatsItem = new Fats();

            foreach (var fat in listOfFats)
            {
                try
                {
                    var fatElement = document.QuerySelector($"td:has(a[href*='{fat}']) + .text-right");

                    if (fatElement != null)
                    {
                        var fatValue = fatElement.TextContent.Trim();

                        fatValue = fatValue.Split(' ')[0];
                        var parsedFatValue = TryParseFloat(fatValue);

                        switch (fat)
                        {
                            case "lipids":
                                fatsItem.TotalFats = parsedFatValue;
                                break;

                            case "monounsaturated-acids":
                                fatsItem.MonounsaturatedFats = parsedFatValue;
                                break;

                            case "polyunsaturated-acids":
                                fatsItem.PolyunsaturatedFats = parsedFatValue;
                                break;

                            case "saturated-acids":
                                fatsItem.SaturatedFats = parsedFatValue;
                                break;

                            case "trans-acids":
                                fatsItem.TransFats = parsedFatValue;
                                break;
                        }
                    }
                }
                catch
                {
                    Console.WriteLine("Could not add the fat!");
                }
            }

            return fatsItem;
        }

        private Minerals CreateMineralsItem(IHtmlDocument document)
        {
            var listOfMinerals = new List<string> { "iron", "potassium", "calcium",
        "magnesium", "manganese", "copper",
        "sodium", "selenium", "fluoride",
        "phosphorus", "zinc" };

            var mineralsItem = new Minerals();

            foreach (var mineral in listOfMinerals)
            {
                try
                {
                    var mineralElement = document.QuerySelector($"td:has(a[href*='{mineral}']) + .text-right");

                    if (mineralElement != null)
                    {
                        var mineralValue = mineralElement.TextContent.Trim();

                        mineralValue = mineralValue.Split(' ')[0];
                        var parsedMineralValue = TryParseFloat(mineralValue);

                        switch (mineral)
                        {
                            case "iron":
                                mineralsItem.Iron = parsedMineralValue;
                                break;

                            case "potassium":
                                mineralsItem.Potassium = parsedMineralValue;
                                break;

                            case "calcium":
                                mineralsItem.Calcium = parsedMineralValue;
                                break;

                            case "magnesium":
                                mineralsItem.Magnesium = parsedMineralValue;
                                break;

                            case "manganese":
                                mineralsItem.Manganese = parsedMineralValue;
                                break;

                            case "copper":
                                mineralsItem.Copper = parsedMineralValue;
                                break;

                            case "sodium":
                                mineralsItem.Sodium = parsedMineralValue;
                                break;

                            case "selenium":
                                mineralsItem.Selenium = parsedMineralValue;
                                break;

                            case "fluoride":
                                mineralsItem.Fluoride = parsedMineralValue;
                                break;

                            case "phosphorus":
                                mineralsItem.Phosphorus = parsedMineralValue;
                                break;

                            case "zinc":
                                mineralsItem.Zinc = parsedMineralValue;
                                break;
                        }
                    }
                }
                catch
                {
                    Console.WriteLine("Could not add the mineral!");
                }
            }

            return mineralsItem;
        }

        private Sterols CreateSterolsItem(IHtmlDocument document)
        {
            var listOfSterols = new List<string> { "cholesterol", "phytosterols",
        "stigmasterol", "campesterol", "beta-sitosterol" };

            var sterolsItem = new Sterols();

            foreach (var sterol in listOfSterols)
            {
                try
                {
                    var sterolElement = document.QuerySelector($"td:has(a[href*='{sterol}']) + .text-right");

                    if (sterolElement != null)
                    {
                        var sterolValue = sterolElement.TextContent.Trim();

                        // Extract only the numerical value before the unit (if any)
                        sterolValue = sterolValue.Split(' ')[0];
                        var parsedSterolValue = TryParseFloat(sterolValue);

                        switch (sterol)
                        {
                            case "cholesterol":
                                sterolsItem.Cholesterol = parsedSterolValue;
                                break;

                            case "phytosterols":
                                sterolsItem.Phytosterols = parsedSterolValue;
                                break;

                            case "stigmasterol":
                                sterolsItem.Stigmasterols = parsedSterolValue;
                                break;

                            case "campesterol":
                                sterolsItem.Campesterol = parsedSterolValue;
                                break;

                            case "beta-sitosterol":
                                sterolsItem.BetaSitosterols = parsedSterolValue;
                                break;
                        }
                    }
                }
                catch
                {
                    Console.WriteLine("Could not add the sterol!");
                }
            }

            return sterolsItem;
        }

        private Other CreateOtherItem(IHtmlDocument document)
        {
            var listOfOther = new List<string> { "alcohol-ethyl", "water", "caffeine",
        "theobromine", "ash" };

            var otherItem = new Other();

            foreach (var item in listOfOther)
            {
                try
                {
                    var otherElement = document.QuerySelector($"td:has(a[href*='{item}']) + .text-right");

                    if (otherElement != null)
                    {
                        var otherValue = otherElement.TextContent.Trim();

                        // Extract only the numerical value before the unit (if any)
                        otherValue = otherValue.Split(' ')[0];
                        var parsedOtherValue = TryParseFloat(otherValue);

                        switch (item)
                        {
                            case "alcohol-ethyl":
                                otherItem.Alcohol = parsedOtherValue;
                                break;

                            case "water":
                                otherItem.Water = parsedOtherValue;
                                break;

                            case "caffeine":
                                otherItem.Caffeine = parsedOtherValue;
                                break;

                            case "theobromine":
                                otherItem.Theobromine = parsedOtherValue;
                                break;

                            case "ash":
                                otherItem.Ash = parsedOtherValue;
                                break;
                        }
                    }
                }
                catch
                {
                    Console.WriteLine("Could not add the 'other' item!");
                }
            }

            return otherItem;
        }


        private string CleanText(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return null;
            }

            string pattern = @"\s*([\p{L}\p{M}]+(?:[,\s]+[\p{L}\p{M}]+)*)\s*";
            var match = Regex.Match(input, pattern);

            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }

            return input.Trim();
        }


        float? TryParseFloat(string input)
        {
            if (float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
            {
                return result;
            }
            return null;
        }
    }
}
