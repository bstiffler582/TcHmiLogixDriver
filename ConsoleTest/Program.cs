using LogixDriver;
using System.Text.Json;

var target = new LogixTarget("Test", "192.168.68.64");

var loader = new LogixTagLoader();
//var tagList = loader.LoadTags(target);

//target.AddTagDefinition(tagList);

//foreach (var def in target.TagDefinitionsFlat)
//    Console.WriteLine($"Path: {def.Key}; Type: {def.Value.Type.Name}; Length: {def.Value.Type.Length}; Offset: {def.Value.Offset}");

////File.WriteAllText("tags.json", JsonSerializer.Serialize(target.TagDefinitions, new JsonSerializerOptions() { WriteIndented = true }));

//var res = loader.ReadTagValue(target, "structArray");
//Console.WriteLine(JsonSerializer.Serialize(res));

var info = loader.ReadControllerInfo(target);
Console.WriteLine(info);