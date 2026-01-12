using Logix;

var target = new LogixTarget("Test", "192.168.68.64");
var driver = new LogixDriver(target);

driver.LoadTags();

foreach (var def in target.TagDefinitionsFlat)
    Console.WriteLine($"Path: {def.Key}; Type: {def.Value.Type.Name}; Length: {def.Value.Type.Length}; Offset: {def.Value.Offset}; BitOffset: {def.Value.BitOffset}");

//var test = target.TagDefinitions;
//File.WriteAllText("tags.json", JsonSerializer.Serialize(target.TagDefinitions, new JsonSerializerOptions() { WriteIndented = true }));

//var res = driver.ReadTagValue("Program:MainProgram.prgTmr");
//Console.WriteLine(JsonSerializer.Serialize(res));

var info = driver.ReadControllerInfo();
Console.WriteLine(info);

// 2D:
// when reading root (e.g. arr2D), need to set tag elementcount to total elements (dim * dim)
// when reading sub-elements (e.g. arr2D[1]), need to read from 0th child element (arr2D[1][0]) and set element count to 5
// when reading inner-most element, works appropriately

//var res = driver.ReadTagValue("Program:MainProgram.arr2D[1][2]");
var res = driver.ReadTagValue("structArray");
Console.WriteLine(res);

//driver.WriteTagValue("ctrlrDint", 25);