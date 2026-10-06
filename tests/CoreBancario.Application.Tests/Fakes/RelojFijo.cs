namespace CoreBancario.Application.Tests.Fakes;

public sealed class RelojFijo(DateTimeOffset instante) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => instante;
}
