using Logix;
using System.Text.Json;

var target = new LogixTarget("Test", "192.168.68.64");
var driver = new LogixDriver(target);

driver.LoadTags();

//foreach (var def in target.TagDefinitionsFlat)
//    Console.WriteLine($"Path: {def.Key}; Type: {def.Value.Type.Name}; Length: {def.Value.Type.Length}; Offset: {def.Value.Offset}");

//var test = target.TagDefinitions;

//File.WriteAllText("tags.json", JsonSerializer.Serialize(target.TagDefinitions, new JsonSerializerOptions() { WriteIndented = true }));

//var res = driver.ReadTagValue("structArray");
//Console.WriteLine(JsonSerializer.Serialize(res));

var info = driver.ReadControllerInfo();
Console.WriteLine(info);

//var res = driver.ReadTagValue("ctrlrDint");
//Console.WriteLine(res);

driver.WriteTagValue("ctrlrDint", 25);