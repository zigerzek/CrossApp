using System.Linq;
using System.Text.Json;
using Core;

// Дані, що не стосуються середовища виконання, — це метадані застосунку,
// а не "інформація про середовище", тому вони лишаються тут, а не в Core.
const string Student = "Struminskyi Zakharii, FEI-32s";
const string Domain = "Warehouse (goods, batches, balances, transfers)";

EnvironmentReport report = EnvironmentInfo.Collect();

if (args.Contains("--json"))
{
    var payload = new
    {
        Student,
        Domain,
        report.OSDescription,
        report.EnvironmentOS,
        report.Architecture,
        report.DotNetVersion,
        report.Runtime,
        report.AppDirectory,
        report.CurrentDirectory,
        report.DetectedRid,
        report.ReportedRid,
        report.BuildNote
    };

    string jsonString = JsonSerializer.Serialize(payload);
    Console.WriteLine(jsonString);
}
else
{
    Console.WriteLine("CrossApp - Cross-Platform Programming Workshop");
    Console.WriteLine($"Student: {Student}");
    Console.WriteLine(new string('-', 52));
    Console.WriteLine($"OS (OSDescription) : {report.OSDescription}");
    Console.WriteLine($"OS (Environment)   : {report.EnvironmentOS}");
    Console.WriteLine($"Process Arch       : {report.Architecture}");
    Console.WriteLine($".NET Version (CLR) : {report.DotNetVersion}");
    Console.WriteLine($"Runtime            : {report.Runtime}");
    Console.WriteLine($"App Directory      : {report.AppDirectory}");
    Console.WriteLine($"Current Directory  : {report.CurrentDirectory}");
    Console.WriteLine($"RID (visznacheno)  : {report.DetectedRid}");
    Console.WriteLine($"RID (vid .NET)     : {report.ReportedRid}");
    Console.WriteLine($"Build Note (TFM)   : {report.BuildNote}");
    Console.WriteLine(new string('-', 52));
    Console.WriteLine($"Domain Area        : {Domain}");
}