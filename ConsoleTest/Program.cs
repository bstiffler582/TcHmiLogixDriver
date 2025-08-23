using ConsoleTest;
using libplctag;

var target = new LogixTarget("Test", "192.168.68.64", "1,0", PlcType.ControlLogix, Protocol.ab_eip);

LogixDriver driver = LogixDriver.Instance;

var tags = driver.LoadTags(target);

driver.PrintTags(tags);