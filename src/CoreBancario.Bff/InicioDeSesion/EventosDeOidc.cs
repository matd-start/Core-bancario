using CoreBancario.Bff.Configuracion;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace CoreBancario.Bff.InicioDeSesion;

/// <summary>Eventos de los dos esquemas OIDC; uno por área.</summary>
public sealed class EventosDeOidc : OpenIdConnectEvents
{
    // SDD: esqueleto creado por test-writer
    public EventosDeOidc(AreaDeSesion area)
    {
    }

    /// <summary>prompt=login (CL-09), salvo Items[Accion] = "UPDATE_PASSWORD": entonces kc_action=UPDATE_PASSWORD y sin prompt.</summary>
    public override Task RedirectToIdentityProvider(RedirectContext context) => throw new NotImplementedException();

    /// <summary>RNF-05: ProtocolMessage.Iss distinto del Emisor del área, o ausente, hace context.Fail.</summary>
    public override Task MessageReceived(MessageReceivedContext context) => throw new NotImplementedException();

    /// <summary>Con Items[Accion]: RedirectUri según kc_action_status. Siempre: Items[Area].</summary>
    public override Task TokenValidated(TokenValidatedContext context) => throw new NotImplementedException();

    /// <summary>RN-26: mismo sid conserva Inicio; otro sid cierra la sesión anterior.</summary>
    public override Task TicketReceived(TicketReceivedContext context) => throw new NotImplementedException();

    /// <summary>CL-01/CL-02: HandleResponse(); con sesión vigente a su inicio; si no, al aviso inicio-fallido.</summary>
    public override Task RemoteFailure(RemoteFailureContext context) => throw new NotImplementedException();
}
