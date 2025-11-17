using ConsoleTest;
using System.Text.Json;

var target = new LogixTarget("Test", "192.168.68.64");

LogixDriver driver = new LogixDriver();
var tagList = driver.LoadTags(target);

target.AddTagDefinition(tagList);

foreach (var def in target.TagDefinitionsFlat)
    Console.WriteLine($"Path: {def.Key}; Type: {def.Value.Type.Name}; Length: {def.Value.Type.Length}; Offset: {def.Value.Offset}");

//File.WriteAllText("tags.json", JsonSerializer.Serialize(target.TagDefinitions, new JsonSerializerOptions() { WriteIndented = true }));

var res = driver.ReadTagValue(target, "structArray");
Console.WriteLine(JsonSerializer.Serialize(res));