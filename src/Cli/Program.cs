using System.Linq;
using System.Text.Json;
using Core;
using Core.Dto;
using Core.Import;

string path = args.Length > 0 && !args[0].StartsWith("--")
    ? args[0]
    : Path.Combine("data", "sample.csv");

if (!File.Exists(path))
{
    Console.WriteLine($"Файл не знайдено: {Path.GetFullPath(path)}");
    return 1;
}

ImportResult<GoodsDto> result = Path.GetExtension(path).ToLowerInvariant() switch
{
    ".json" => GoodsJsonImporter.Load(path),
    ".csv" => GoodsCsvImporter.Load(path),
    var ext => throw new NotSupportedException($"Розширення '{ext}' не підтримується")
};

Console.WriteLine($"Завантажено записів: {result.Items.Count}");
foreach (GoodsDto g in result.Items.Take(5))
    Console.WriteLine($"  {g.Id,-6} {g.Sku,-10} {g.Name,-26} {g.Quantity,5} {g.Unit}");

if (result.Errors.Count > 0)
{
    Console.WriteLine($"Пропущено рядків: {result.Errors.Count}");
    foreach (string e in result.Errors)
        Console.WriteLine($"  ! {e}");
}


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

return 0;