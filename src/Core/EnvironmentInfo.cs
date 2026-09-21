using System.Runtime.InteropServices;

namespace Core;

// Дані про середовище виконання. Record, бо це незмінний "знімок" вимірювання,
// а не сутність з поведінкою — саме тому тут немає методів, лише властивості.
public sealed record EnvironmentReport(
    string OSDescription,
    string EnvironmentOS,
    string Architecture,
    string DotNetVersion,
    string Runtime,
    string AppDirectory,
    string CurrentDirectory,
    string DetectedRid,
    string ReportedRid,
    string BuildNote);

// Клас, а не record: тут є поведінка (алгоритм збору даних), а не самі дані.
public static class EnvironmentInfo
{
#if NET10_0_OR_GREATER
    public const string BuildNote = "збірка під net10.0";
#else
    public const string BuildNote = "збірка під net8.0";
#endif

    public static EnvironmentReport Collect() => new(
        OSDescription: RuntimeInformation.OSDescription,
        EnvironmentOS: Environment.OSVersion.ToString(),
        Architecture: RuntimeInformation.ProcessArchitecture.ToString(),
        DotNetVersion: Environment.Version.ToString(),
        Runtime: RuntimeInformation.FrameworkDescription,
        AppDirectory: AppContext.BaseDirectory,
        CurrentDirectory: Environment.CurrentDirectory,
        DetectedRid: DetectRid(),
        ReportedRid: RuntimeInformation.RuntimeIdentifier,
        BuildNote: BuildNote);

    // Ручне визначення RID: показує, з чого складається рядок на кшталт win-x64.
    private static string DetectRid()
    {
        string os =
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win" :
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux" :
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx" : "unknown";

        string arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            _ => "unknown"
        };

        return $"{os}-{arch}";
    }
}