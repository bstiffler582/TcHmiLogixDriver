using ConsoleTest;

var target = new LogixTarget("Test", "192.168.68.64");

LogixDriver driver = new LogixDriver();

target.AddTagDefinition(driver.LoadTags(target));

foreach (var def in target.TagDefinitions)
    Console.WriteLine($"Path: {def.Key}; Type: {def.Value.Type.Name}");

var res = driver.ReadTagValue(target, "Program:MainProgram.fbTest.fbFloat");
Console.WriteLine(res);