using Logix;
using Logix.Tags;
using Logix.Test;
using System.Text.Json;

var target = new LogixTarget("Test", "192.168.68.64");
var driver = new LogixDriver(target);

// offline testing
var tagMap = JsonSerializer.Deserialize<Dictionary<string, List<TagInfo>>>(File.ReadAllText(@"Test\OfflineTags.json"));
var udtMap = JsonSerializer.Deserialize<Dictionary<ushort, UdtInfo>>(File.ReadAllText(@"Test\OfflineUdts.json"));

driver.TagReader = new TestLogixTagReader(tagMap, udtMap);

driver.LoadTags();

foreach (var def in target.TagDefinitionsFlat)
    Console.WriteLine($"Path: {def.Key}; Type: {def.Value.Type.Name}; Length: {def.Value.Type.Length}; Offset: {def.Value.Offset}; BitOffset: {def.Value.BitOffset}");

//var test = target.TagDefinitions;
//File.WriteAllText("tags.json", JsonSerializer.Serialize((driver.TagReader as LogixTagReader).tagMap, new JsonSerializerOptions() { WriteIndented = true }));
//File.WriteAllText("udts.json", JsonSerializer.Serialize((driver.TagReader as LogixTagReader).udtMap, new JsonSerializerOptions() { WriteIndented = true }));

//var res = driver.ReadTagValue("Program:MainProgram.prgTmr");
//Console.WriteLine(JsonSerializer.Serialize(res));

var info = driver.ReadControllerInfo();
Console.WriteLine(info);

//var res = driver.ReadTagValue("Program:MainProgram.arr2D[1][2]");
//var res = driver.ReadTagValue("structArray");
//Console.WriteLine(res);

//driver.WriteTagValue("ctrlrDint", 25);