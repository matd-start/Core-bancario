# Principios con ejemplos en C#

Los ejemplos usan un dominio de pedidos. Cada uno muestra el problema, la versión mejorada y cuándo **no** hace falta aplicarlo.

---

## S — Single Responsibility (una sola razón para cambiar)

**Mal:** el servicio valida, guarda, cobra y envía correo. Cambiar el proveedor de correo obliga a tocar la lógica de pedidos.

```csharp
public class OrderService
{
    public async Task PlaceOrderAsync(OrderDto dto)
    {
        if (dto.Items.Count == 0) throw new ArgumentException("Sin productos");
        using var conn = new SqlConnection("...");
        // INSERT ...
        var smtp = new SmtpClient("smtp.empresa.com");
        await smtp.SendMailAsync("ventas@empresa.com", dto.Email, "Pedido", "Gracias");
    }
}
```

**Bien:** el caso de uso coordina; cada detalle vive en su lugar y la notificación reacciona a un evento.

```csharp
public sealed class PlaceOrderHandler(IOrderRepository orders, IEventPublisher events)
{
    public async Task<Guid> HandleAsync(PlaceOrderCommand cmd, CancellationToken ct)
    {
        var order = Order.Create(cmd.CustomerId, cmd.Items); // la entidad valida sus reglas
        await orders.AddAsync(order, ct);
        await events.PublishAsync(new OrderPlaced(order.Id, cmd.CustomerEmail), ct);
        return order.Id;
    }
}
```

**No hace falta** partir una clase de 40 líneas que hace una sola cosa en cinco clases de 8 líneas.

---

## O — Open/Closed (abierto a extensión, cerrado a modificación)

**Mal:** cada tipo de descuento nuevo obliga a editar el mismo `switch`.

```csharp
public decimal Apply(Order order, string type) => type switch
{
    "black-friday" => order.Total * 0.7m,
    "vip"          => order.Total * 0.9m,
    _              => order.Total
};
```

**Bien:** agregar una política nueva es agregar una clase.

```csharp
public interface IDiscountPolicy
{
    bool AppliesTo(Order order);
    decimal Apply(decimal total);
}

public sealed class VipDiscount : IDiscountPolicy
{
    public bool AppliesTo(Order order) => order.Customer.IsVip;
    public decimal Apply(decimal total) => total * 0.9m;
}
```

**No hace falta** si hay dos casos estables que no van a crecer: el `switch` es más simple (KISS).

---

## L — Liskov Substitution (los hijos deben poder reemplazar al padre)

**Mal:** el hijo rompe el contrato del padre.

```csharp
public class Product
{
    public virtual ShippingLabel Ship() => new(/* ... */);
}

public class DigitalProduct : Product
{
    public override ShippingLabel Ship() => throw new NotSupportedException(); // sorpresa
}
```

**Bien:** modelar la capacidad por separado en lugar de forzar la herencia.

```csharp
public abstract class Product { /* nombre, precio */ }
public interface IShippable { ShippingLabel Ship(); }

public sealed class PhysicalProduct : Product, IShippable { public ShippingLabel Ship() => new(/* ... */); }
public sealed class DigitalProduct : Product { }
```

Señal de alarma: un `override` que lanza `NotSupportedException` o que ignora lo que el padre prometía.

---

## I — Interface Segregation (interfaces pequeñas y específicas)

**Mal:** quien solo envía correos depende también de SMS y push.

```csharp
public interface INotificationService
{
    Task SendEmailAsync(string to, string body);
    Task SendSmsAsync(string phone, string body);
    Task SendPushAsync(Guid userId, string body);
}
```

**Bien:**

```csharp
public interface IEmailSender { Task SendAsync(string to, string subject, string body, CancellationToken ct); }
public interface ISmsSender   { Task SendAsync(string phone, string body, CancellationToken ct); }
```

---

## D — Dependency Inversion (depender de abstracciones que define quien las usa)

**Mal:** la capa de aplicación conoce SQL Server.

```csharp
public class GetOrderHandler
{
    private readonly SqlOrderRepository _repo = new(); // acoplado a infraestructura
}
```

**Bien:** Application define el puerto, Infrastructure lo implementa y la inyección de dependencias los une.

```csharp
// Application
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(Order order, CancellationToken ct);
}

// Infrastructure
public sealed class EfOrderRepository(AppDbContext db) : IOrderRepository { /* ... */ }

// Api / Program.cs
builder.Services.AddScoped<IOrderRepository, EfOrderRepository>();
```

---

## KISS — Keep It Simple

**Mal:** anidación que obliga a leer todo para entender un caso.

```csharp
if (order != null)
{
    if (order.Items.Any())
    {
        if (order.Status == OrderStatus.Pending)
        {
            order.Confirm();
        }
    }
}
```

**Bien:** guard clauses.

```csharp
if (order is null) return Result.NotFound();
if (!order.Items.Any()) return Result.Invalid("El pedido no tiene productos");
if (order.Status != OrderStatus.Pending) return Result.Invalid("El pedido ya fue procesado");

order.Confirm();
```

También es KISS no crear una fábrica, un builder y una estrategia para algo que un constructor resuelve.

---

## DRY — Don't Repeat Yourself (el conocimiento, no las líneas)

**Mal:** la regla de qué es un correo válido está copiada en tres validadores.

**Bien:** vive en un solo lugar, el value object.

```csharp
public sealed record Email
{
    public string Value { get; }

    private Email(string value) => Value = value;

    public static Email Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.Contains('@'))
            throw new DomainException("Correo inválido");
        return new Email(value.Trim().ToLowerInvariant());
    }
}
```

**Cuidado:** dos métodos que se parecen pero representan reglas de negocio distintas (por ejemplo, el cálculo de IVA y el de comisión) **no** se deben unir. Si mañana cambia una, la otra se rompe.

---

## YAGNI — You Aren't Gonna Need It

**Mal:** preparar el sistema para cinco bases de datos, un sistema de plugins y configuración por tenant cuando solo existe SQL Server y un cliente.

**Bien:** implementar lo que el requisito pide hoy, con código limpio. Un código simple y bien separado es fácil de extender cuando el requisito llegue de verdad.

Pregunta de control: "¿Qué requisito concreto de hoy pide esta abstracción?" Si la respuesta es "algún día…", no se agrega.
