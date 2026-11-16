namespace Cli.Commands;

internal static class ArgumentSplitter
{
    /// <summary>Відокремлює команду (позиційний аргумент) від прапорців сховища.</summary>
    public static (string Command, string[] StoreArguments) Split(string[] args)
    {
        string? command = null;
        var storeArguments = new List<string>();
        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            if (argument == "--path")
            {
                storeArguments.Add(argument);
                if (index + 1 < args.Length) storeArguments.Add(args[++index]);
                continue;
            }

            if (argument.StartsWith("--", StringComparison.Ordinal))
            {
                storeArguments.Add(argument);
                continue;
            }

            if (command is not null)
                throw new ArgumentException("Укажіть лише одну команду.");
            command = argument;
        }

        return (command ?? "report-top", storeArguments.ToArray());
    }
}
