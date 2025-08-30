using ConsoleTest;
using System.Text.Json;

var target = new LogixTarget("Test", "192.168.68.64");

LogixDriver driver = new LogixDriver();

target.AddTagDefinition(driver.LoadTags(target));

foreach (var def in target.TagDefinitionsFlat)
    Console.WriteLine($"Path: {def.Key}; Type: {def.Value.Type.Name}; Length: {def.Value.Type.Length}; Offset: {def.Value.Offset}");

var res = driver.ReadTagValue(target, "Program:MainProgram.prgReadTest");
Console.WriteLine(JsonSerializer.Serialize(res));