namespace GestaoEstoque.BuildingBlocks;

public interface IDomainEvent
{
    DateTime OcorridoEm { get; }
}

public abstract class Entity
{
    private readonly List<IDomainEvent> _eventos = [];

    public Guid Id { get; protected set; } = Guid.NewGuid();

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _eventos;

    protected void Raise(IDomainEvent evento) => _eventos.Add(evento);

    public void LimparEventos() => _eventos.Clear();
}

public class DomainException(string mensagem) : Exception(mensagem);
