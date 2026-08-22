using System;

namespace Igra.Client.Core;

/// <summary>
/// Отладочные флаги. По умолчанию всё выключено.
/// Включить на время разработки: установить переменную окружения IGRA_DEBUG=1.
/// </summary>
public static class DebugConfig
{
    public static readonly bool Enabled =
        Environment.GetEnvironmentVariable("IGRA_DEBUG") == "1";
}
