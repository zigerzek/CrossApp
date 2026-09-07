using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;

var sysInfo = new 
{
    Student = "Struminskyi Zakharii, FEI-32s",
    OSDescription = RuntimeInformation.OSDescription,
    EnvironmentOS = Environment.OSVersion.ToString(),
    Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
    DotNetVersion = Environment.Version.ToString(),
    Runtime = RuntimeInformation.FrameworkDescription,
    AppDirectory = AppContext.BaseDirectory,
    CurrentDirectory = Environment.CurrentDirectory,
    Domain = "Warehouse (goods, batches, balances, transfers)" 
};

if (args.Contains("--json"))
{
    string jsonString = JsonSerializer.Serialize(sysInfo);
    Console.WriteLine(jsonString);
}
else
{
    Console.WriteLine("CrossApp - Cross-Platform Programming Workshop");
    Console.WriteLine($"Student: {sysInfo.Student}");
    Console.WriteLine(new string('-', 52));
    Console.WriteLine($"OS (OSDescription): {sysInfo.OSDescription}");
    Console.WriteLine($"OS (Environment)  : {sysInfo.EnvironmentOS}");
    Console.WriteLine($"Process Arch      : {sysInfo.Architecture}");
    Console.WriteLine($".NET Version (CLR): {sysInfo.DotNetVersion}");
    Console.WriteLine($"Runtime           : {sysInfo.Runtime}");
    Console.WriteLine($"App Directory     : {sysInfo.AppDirectory}");
    Console.WriteLine($"Current Directory : {sysInfo.CurrentDirectory}");
    Console.WriteLine(new string('-', 52));
    Console.WriteLine($"Domain Area       : {sysInfo.Domain}");
}