using Logix.Proto;
using Logix.Tags;
using Logix.Test;
using System.Text.Json;

var driver = new Driver("test", "192.168.68.64", "1,0");

// offline testing
//var tagMap = JsonSerializer.Deserialize<Dictionary<string, List<TagInfo>>>(File.ReadAllText(@"Test\OfflineTags.json"));
//var udtMap = JsonSerializer.Deserialize<Dictionary<ushort, UdtInfo>>(File.ReadAllText(@"Test\OfflineUdts.json"));

//driver.TagReader = new TestLogixTagReader(tagMap, udtMap);
//var tags = new List<string>() { "Program:FP01L.Main.I.Station[0]", "Program:MainProgram.prgTmr" };
//driver.LoadTags();

//driver.LoadTags(new List<string>() { "structArray" });

//var test = target.TagDefinitions;
//File.WriteAllText("tags.json", JsonSerializer.Serialize((driver.TagReader as LogixTagReader).tagMap, new JsonSerializerOptions() { WriteIndented = true }));
//File.WriteAllText("udts.json", JsonSerializer.Serialize((driver.TagReader as LogixTagReader).udtMap, new JsonSerializerOptions() { WriteIndented = true }));

//var res = driver.ReadTagValue("ctrlrReadTest");
//Console.WriteLine(JsonSerializer.Serialize(res));

//foreach (var def in target.TagDefinitionsFlat)
//    Console.WriteLine($"Path: {def.Key}; Type: {def.Value.TypeName}; Length: {def.Value.Length}; Offset: {def.Value.Offset}; BitOffset: {def.Value.BitOffset}");

var info = driver.ReadControllerInfo();
Console.WriteLine(info);

//var res = driver.ReadTagValue("Program:MainProgram.arr2D[1][2]");
//var res = driver.ReadTagValue("structArray");
//Console.WriteLine(res);

//driver.WriteTagValue("ctrlrDint", 25);