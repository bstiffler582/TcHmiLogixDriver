using Logix.Driver;
using System.Text.Json;

var driver = new Driver("test", "192.168.68.64", "1,0");

// offline testing
//var tagMap = JsonSerializer.Deserialize<Dictionary<string, List<TagInfo>>>(File.ReadAllText(@"Test\OfflineTags.json"));
//var udtMap = JsonSerializer.Deserialize<Dictionary<ushort, UdtInfo>>(File.ReadAllText(@"Test\OfflineUdts.json"));

//driver.TagReader = new TestLogixTagReader(tagMap, udtMap);
//var tags = new List<string>() { "Program:FP01L", "Program:FP02L" };
//await driver.LoadTagsAsync(tags);

driver.LoadTags(new List<string>() { "writeAlias" });

//var test = target.TagDefinitions;
//File.WriteAllText("tags.json", JsonSerializer.Serialize((driver.TagReader as LogixTagReader).tagMap, new JsonSerializerOptions() { WriteIndented = true }));
//File.WriteAllText("udts.json", JsonSerializer.Serialize((driver.TagReader as LogixTagReader).udtMap, new JsonSerializerOptions() { WriteIndented = true }));

var res1 = await driver.ReadTagValueAsync("ctrlrWriteTest");
Console.WriteLine(JsonSerializer.Serialize(res1));

var res2 = await driver.ReadTagValueAsync("ctrlrReadTest");
Console.WriteLine(JsonSerializer.Serialize(res2));

foreach (var def in driver.GetTagDefinitionsFlat())
    Console.WriteLine($"Path: {def.Key}; Type: {def.Value.TypeName}; Length: {def.Value.Length}; Offset: {def.Value.Offset}; BitOffset: {def.Value.BitOffset}");

var info = driver.ReadControllerInfo();
Console.WriteLine(info);

//var res = driver.ReadTagValue("Program:MainProgram.arr2D[1][2]");
//var res = driver.ReadTagValue("structArray");
//Console.WriteLine(res);

//driver.WriteTagValue("ctrlrDint", 25);