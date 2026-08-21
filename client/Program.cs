using Igra.Client.Core;
using Igra.Client.Net;

// Режимы:
//   dotnet run --project client                — окно игры
//   dotnet run --project client -- --autotest  — сетевой автотест без окна (проверка протокола)

var argsList = args.ToList();
int serverIdx = argsList.IndexOf("--server");
if (serverIdx >= 0 && serverIdx + 1 < argsList.Count)
    NetClient.DefaultServerUrl = argsList[serverIdx + 1];

if (argsList.Contains("--autotest"))
{
    return await Autotest.RunAsync();
}

using var game = new IgraGame();
game.Run();
return 0;
