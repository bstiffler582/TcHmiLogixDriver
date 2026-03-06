using Logix.Driver;
using System.Text.Json;

var driver = Driver.Create(new Target("test", "192.168.68.64", "1,0"));
driver.TryConnect();

// offline testing
//var tagMap = JsonSerializer.Deserialize<Dictionary<string, List<TagInfo>>>(File.ReadAllText(@"Test\OfflineTags.json"));
//var udtMap = JsonSerializer.Deserialize<Dictionary<ushort, UdtInfo>>(File.ReadAllText(@"Test\OfflineUdts.json"));

//driver.TagReader = new TestLogixTagReader(tagMap, udtMap);
//var tags = new List<string>() { "Program:FP01L", "Program:FP02L" };
//await driver.LoadTagsAsync(tags);

driver.LoadTags();

foreach (var def in driver.GetTagDefinitionsFlat().Where(t => t.Key.StartsWith("Program:FP01L")))
    Console.WriteLine($"Path: {def.Key}; Type: {def.Value.TypeName}; Length: {def.Value.Length}; Offset: {def.Value.Offset}; BitOffset: {def.Value.BitOffset}");

var res1 = await driver.ReadTagValueAsync("Program:FP01L.Main.I.Station[0]");
Console.WriteLine(JsonSerializer.Serialize(res1));

var info = driver.ControllerInfo;
Console.WriteLine(info);

//var res = driver.ReadTagValue("Program:MainProgram.arr2D[1][2]");
//var res = driver.ReadTagValue("structArray");
//Console.WriteLine(res);

//driver.WriteTagValue("ctrlrDint", 25);