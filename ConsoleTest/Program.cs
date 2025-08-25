using ConsoleTest;

var target = new LogixTarget("Test", "192.168.68.64");

LogixDriver driver = new LogixDriver();

target.AddTags(driver.LoadTags(target));

driver.PrintTags(target.Tags);