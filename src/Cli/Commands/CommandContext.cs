using Cli.Configuration;
using Cli.Logging;

namespace Cli.Commands;

internal sealed record CommandContext(AppSettings Settings, ConsoleLog Log, CliArguments Args, CancellationToken Ct);
