using System.Collections.Concurrent;

namespace CoreBancario.Bff.Tests;

public sealed record RegistroDeLog(string Categoria, LogLevel Nivel, string Mensaje, Exception? Excepcion);

/// <summary>
/// Captura los logs del BFF en memoria para RNF-01 y RNF-04. Las pruebas corren en paralelo contra la
/// misma API: cada una busca SUS registros por un dato unico (el sub del operador, la ruta con un Guid).
/// </summary>
public sealed class ProveedorDeLogsEnMemoria : ILoggerProvider
{
    private readonly ConcurrentQueue<RegistroDeLog> _registros = new();

    public IReadOnlyList<RegistroDeLog> Registros => [.. _registros];

    public IEnumerable<RegistroDeLog> QueContienen(string texto) =>
        Registros.Where(r => r.Mensaje.Contains(texto, StringComparison.Ordinal)
                             || (r.Excepcion?.ToString().Contains(texto, StringComparison.Ordinal) ?? false));

    public ILogger CreateLogger(string categoryName) => new Registrador(categoryName, _registros);

    public void Dispose()
    {
    }

    private sealed class Registrador(string categoria, ConcurrentQueue<RegistroDeLog> registros) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            registros.Enqueue(new RegistroDeLog(categoria, logLevel, formatter(state, exception), exception));
    }
}
