using ConsoleTest;
using libplctag;

var target = new LogixTarget("Test", "192.168.68.64");

LogixDriver driver = new LogixDriver();

var tags = driver.LoadTags(target);

driver.PrintTags(tags);